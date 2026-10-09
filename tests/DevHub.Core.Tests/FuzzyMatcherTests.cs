using DevHub.Core.Search;
using Xunit;

namespace DevHub.Core.Tests;

public class FuzzyMatcherTests
{
    // ----- Does it match at all? -----

    [Theory]
    [InlineData("push", "Git: Push")]
    [InlineData("gp", "Git: Push")]
    [InlineData("GITPUSH", "Git: Push")]
    [InlineData("git push", "Git: Push")]
    [InlineData("rr", "Rescan Repositories")]
    [InlineData("ow", "OpenWith")]
    public void Matches_when_pattern_is_an_in_order_subsequence(string pattern, string text)
    {
        Assert.NotNull(FuzzyMatcher.Match(pattern, text));
    }

    [Theory]
    [InlineData("pg", "Git: Push")]        // right letters, wrong order
    [InlineData("pushx", "Git: Push")]     // extra letter
    [InlineData("toolong", "short")]
    [InlineData("x", "")]
    public void Returns_null_when_pattern_is_not_a_subsequence(string pattern, string text)
    {
        Assert.Null(FuzzyMatcher.Match(pattern, text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_pattern_matches_everything_with_score_zero(string pattern)
    {
        var match = FuzzyMatcher.Match(pattern, "Anything");

        Assert.NotNull(match);
        Assert.Equal(0, match.Score);
        Assert.Empty(match.Positions);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => FuzzyMatcher.Match(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => FuzzyMatcher.Match("x", null!));
    }

    // ----- Which characters does it pick (for highlighting)? -----

    [Fact]
    public void Picks_word_starts_over_earlier_letters_inside_words()
    {
        // The 'p' in "Push" is a word start; there is no earlier 'p', but the 's' check
        // below makes sure later word starts are preferred over mid-word letters.
        var match = FuzzyMatcher.Match("gp", "Git: Push");

        Assert.Equal([0, 5], match!.Positions);
    }

    [Fact]
    public void Picks_the_word_start_even_when_an_earlier_mid_word_letter_exists()
    {
        // "Close Settings": the 's' in "Close" comes first, but "Settings" starts a word.
        var match = FuzzyMatcher.Match("cs", "Close Settings");

        Assert.Equal([0, 6], match!.Positions);
    }

    [Fact]
    public void Picks_camelCase_humps()
    {
        var match = FuzzyMatcher.Match("ow", "OpenWith");

        Assert.Equal([0, 4], match!.Positions);
    }

    [Fact]
    public void Prefers_a_consecutive_run()
    {
        // "scan" could take the 's' of "Rescan"'s start... the run "scan" inside
        // "Rescan" (indexes 2..5) is better than scattering across the text.
        var match = FuzzyMatcher.Match("scan", "Rescan Repositories");

        Assert.Equal([2, 3, 4, 5], match!.Positions);
    }

    [Fact]
    public void Positions_skip_ignored_spaces_in_the_pattern()
    {
        var match = FuzzyMatcher.Match("g p", "Git: Push");

        Assert.Equal([0, 5], match!.Positions);
    }

    // ----- Ranking between candidates -----

    [Theory]
    // word-start initials beat the same letters buried mid-word
    [InlineData("gp", "Git: Push", "Merge Upstream")]
    // a prefix beats the same word later in the text
    [InlineData("push", "Push", "Git: Push")]
    // the same run scores more at a word start than mid-word
    [InlineData("scan", "Scan Repositories", "Rescan Repositories")]
    // a consecutive run beats the same letters scattered mid-word
    [InlineData("push", "Git: Push", "pull status hash")]
    // exact word beats a looser spread
    [InlineData("theme", "Toggle Theme", "The Merge")]
    public void Ranks_better_candidate_higher(string pattern, string better, string worse)
    {
        var good = FuzzyMatcher.Match(pattern, better);
        var bad = FuzzyMatcher.Match(pattern, worse);

        Assert.NotNull(good);
        Assert.NotNull(bad);
        Assert.True(good.Score > bad.Score,
            $"'{pattern}': '{better}' scored {good.Score}, '{worse}' scored {bad.Score}");
    }

    [Fact]
    public void Case_does_not_affect_score()
    {
        var lower = FuzzyMatcher.Match("push", "Git: Push");
        var upper = FuzzyMatcher.Match("PUSH", "Git: Push");

        Assert.Equal(lower!.Score, upper!.Score);
    }
}
