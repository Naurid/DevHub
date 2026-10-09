namespace DevHub.Core.Search;

/// <summary>Result of a successful fuzzy match.</summary>
/// <param name="Score">Higher is better. Only meaningful relative to other scores for the same pattern.</param>
/// <param name="Positions">Indexes in the text of each matched pattern character (for highlighting).</param>
public sealed record FuzzyMatch(int Score, IReadOnlyList<int> Positions);

/// <summary>
/// "Find Action"-style fuzzy matching: every pattern character must appear in the text in
/// order (case-insensitive), not necessarily next to each other. "gps" matches "Git: Push".
/// Among all the ways the pattern can line up with the text, the best-scoring one is used:
/// <list type="bullet">
/// <item>characters at the start of a word score extra ("gp" → <b>G</b>it: <b>P</b>ush),</item>
/// <item>runs of consecutive characters score extra ("push" inside "Push" beats p…u…s…h),</item>
/// <item>skipped characters between matches cost a little.</item>
/// </list>
/// Word starts are: the first character, a character after a space or one of <c>_-./:\(</c>,
/// an upper-case letter after a lower-case one (camelCase), and a digit after a letter.
/// </summary>
public static class FuzzyMatcher
{
    // Scoring weights. Word-start and consecutive matches dominate; gaps only break ties.
    // Consecutive must outweigh word start, or "theme" would rank "The Merge" (T-h-e + M-e,
    // two word starts) above "Toggle Theme" (the whole word). Start-of-text is one more
    // than a word start so "Push" beats "Git: Push" for "push".
    // Trade-off: initials ("scan" → Show Changes And Notes) and a run inside a word
    // ("scan" → Rescan) now score about the same; the run wins narrowly. Revisit in the
    // Find Action popup (UI-0 step 10) with real action names before tuning further.
    internal const int MatchScore = 1;
    internal const int StartOfTextBonus = 7;
    internal const int WordStartBonus = 6;
    internal const int ConsecutiveBonus = 7;
    internal const int GapPenalty = 1;
    internal const int MaxGapPenalty = 3;

    private const int NoMatch = int.MinValue;

    /// <summary>
    /// Matches <paramref name="pattern"/> against <paramref name="text"/>.
    /// Returns null when the pattern is not a subsequence of the text.
    /// An empty or whitespace pattern matches everything with score 0 and no positions.
    /// Spaces in the pattern are ignored, so "git push" works like "gitpush".
    /// </summary>
    public static FuzzyMatch? Match(string pattern, string text)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(text);

        var p = pattern.Replace(" ", "");
        if (p.Length == 0)
            return new FuzzyMatch(0, []);
        if (p.Length > text.Length)
            return null;

        // Dynamic programming over (pattern index i, text index j):
        // best[i, j] = best score with pattern[0..i] matched and pattern[i] placed at text[j].
        // from[i, j] = where pattern[i - 1] was placed in that best alignment (for positions).
        int m = p.Length, n = text.Length;
        var best = new int[m, n];
        var from = new int[m, n];

        for (var j = 0; j < n; j++)
        {
            best[0, j] = CharEquals(p[0], text[j]) ? MatchScore + PositionBonus(text, j) : NoMatch;
            from[0, j] = -1;
        }

        for (var i = 1; i < m; i++)
        {
            for (var j = 0; j < n; j++)
            {
                best[i, j] = NoMatch;
                if (j < i || !CharEquals(p[i], text[j]))
                    continue;

                var charScore = MatchScore + PositionBonus(text, j);
                for (var k = i - 1; k < j; k++)
                {
                    if (best[i - 1, k] == NoMatch)
                        continue;

                    var gap = j - k - 1;
                    var link = gap == 0
                        ? ConsecutiveBonus
                        : -Math.Min(gap * GapPenalty, MaxGapPenalty);
                    var score = best[i - 1, k] + charScore + link;

                    if (score > best[i, j])
                    {
                        best[i, j] = score;
                        from[i, j] = k;
                    }
                }
            }
        }

        // Best place for the last pattern character.
        var end = -1;
        for (var j = 0; j < n; j++)
        {
            if (best[m - 1, j] != NoMatch && (end < 0 || best[m - 1, j] > best[m - 1, end]))
                end = j;
        }
        if (end < 0)
            return null;

        var positions = new int[m];
        for (int i = m - 1, j = end; i >= 0; j = from[i, j], i--)
            positions[i] = j;

        return new FuzzyMatch(best[m - 1, end], positions);
    }

    private static bool CharEquals(char a, char b) =>
        char.ToUpperInvariant(a) == char.ToUpperInvariant(b);

    /// <summary>Bonus for where in the text a matched character sits.</summary>
    private static int PositionBonus(string text, int index)
    {
        if (index == 0)
            return StartOfTextBonus;

        var previous = text[index - 1];
        var current = text[index];

        var isWordStart =
            IsSeparator(previous) ||
            (char.IsLower(previous) && char.IsUpper(current)) ||
            (char.IsLetter(previous) && char.IsDigit(current));

        return isWordStart ? WordStartBonus : 0;
    }

    private static bool IsSeparator(char c) =>
        char.IsWhiteSpace(c) || c is '_' or '-' or '.' or '/' or ':' or '\\' or '(';
}
