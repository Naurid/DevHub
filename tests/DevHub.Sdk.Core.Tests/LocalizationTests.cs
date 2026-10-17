using System.Globalization;
using DevHub.Sdk.Core.Formatting;
using DevHub.Sdk.Core.Localization;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

// The language is app-wide (and sets the culture), so these run alone, after the other tests.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LanguageCollection
{
    public const string Name = "Language";
}

[Collection(LanguageCollection.Name)]
public sealed class LocalizationTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public LocalizationTests()
    {
        L.Use(L.French);
        L.Add([new("{0} issues moved to {1}", "{0} tickets déplacés vers {1}"), new("{0:0.#} pts", "{0:0.#} points"), new("a {{b}} {0}", "un {{b}} {0}")]);
    }

    public void Dispose()
    {
        L.Use(L.English);
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = null;
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    [Fact]
    public void Interpolated_text_is_looked_up_with_numbered_values()
    {
        var count = 3;
        var name = "Done";
        Assert.Equal("3 tickets déplacés vers Done", L.T($"{count} issues moved to {name}"));
    }

    [Fact]
    public void Formats_and_braces_survive_the_translation()
    {
        Assert.Equal("1,5 points", L.T($"{1.5:0.#} pts"));   // French culture: decimal comma
        var n = 2;
        Assert.Equal("un {b} 2", L.T($"a {{b}} {n}"));
    }

    [Fact]
    public void Text_without_a_translation_stays_english()
    {
        Assert.Equal("Nothing like this", L.T("Nothing like this"));
        var n = 4;
        Assert.Equal("4 unknown things", L.T($"{n} unknown things"));
    }

    [Fact]
    public void Each_assembly_keeps_its_own_words_for_the_same_english()
    {
        L.Add([new("Low", "Basse")], "DevHub.Board");
        L.Add([new("Low", "Faible")], "DevHub.Board.Risks");
        Assert.Equal("Basse", L.T("Low", "DevHub.Board"));
        Assert.Equal("Faible", L.T("Low", "devhub.board.risks"));   // avares:// addresses lowercase the name
        Assert.Equal("Faible", L.T("Low", "DevHub.Charts"));        // not its own text: all files together, last wins
    }

    [Fact]
    public void Csharp_text_is_looked_up_in_the_calling_assembly_first()
    {
        var me = typeof(LocalizationTests).Assembly.GetName().Name;
        L.Add([new("Committed", "Commité"), new("{0} committed", "{0} commités")], me);
        L.Add([new("Committed", "Engagé"), new("{0} committed", "{0} engagés")], "DevHub.Board.Reports");
        Assert.Equal("Commité", L.T("Committed"));
        var n = 2;
        Assert.Equal("2 commités", L.T($"{n} committed"));
        Assert.Equal("2 commités", L.F("{0} committed", n));
    }

    [Fact]
    public void An_assembly_brings_its_own_translations()
    {
        // Sdk.Core embeds i18n/fr.json.
        var noon = new DateTimeOffset(2026, 10, 17, 12, 0, 0, DateTimeOffset.Now.Offset);   // not "now": past midnight, -25 h is two days back
        Assert.Equal("hier", RelativeTime.Format(noon.AddDays(-1).AddHours(-1), noon));
        Assert.Equal("Barres empilées", L.T("Stacked bars"));
    }
}
