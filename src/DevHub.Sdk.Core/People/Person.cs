using DevHub.Sdk.Core.Search;

namespace DevHub.Sdk.Core.People;

/// <summary>
/// Someone a plugin can assign or mention (an issue's assignee, a reviewer): an id that's stable for the plugin (a GitHub
/// login, an email), a display name, and optionally an email and an avatar URL. The Sdk's PeoplePicker shows and picks them.
/// </summary>
public sealed record Person(string Id, string Name, string? Email = null, string? AvatarUrl = null)
{
    /// <summary>"AL" for Ada Lovelace, "G" for "grace": what an avatar shows without a picture.</summary>
    public string Initials
    {
        get
        {
            var words = Name.Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
            return words.Length switch
            {
                0 => Id.Length > 0 ? Id[..1].ToUpperInvariant() : "?",
                1 => words[0][..1].ToUpperInvariant(),
                _ => (words[0][..1] + words[^1][..1]).ToUpperInvariant(),
            };
        }
    }

    public override string ToString() => Name;
}

/// <summary>Finding people as you type (fuzzy, like Find Action): by name, id or email.</summary>
public static class PeopleSearch
{
    /// <summary>
    /// The people matching <paramref name="query"/>, best first (an empty query: everyone, by name); people in
    /// <paramref name="exclude"/> (already picked) are left out. At most <paramref name="limit"/>.
    /// </summary>
    public static IReadOnlyList<Person> Find(IEnumerable<Person> people, string? query, IEnumerable<Person>? exclude = null, int limit = 20)
    {
        var excluded = new HashSet<string>((exclude ?? []).Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
        var candidates = people.Where(p => !excluded.Contains(p.Id)).DistinctBy(p => p.Id, StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
            return candidates.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).Take(limit).ToList();

        var pattern = query.Trim();
        return candidates
            .Select(p => (Person: p, Score: Best(pattern, p)))
            .Where(m => m.Score is not null)
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.Person.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .Select(m => m.Person)
            .ToList();
    }

    private static int? Best(string pattern, Person person)
    {
        int? best = null;
        foreach (var text in new[] { person.Name, person.Id, person.Email })
        {
            if (text is not null && FuzzyMatcher.Match(pattern, text) is { } match && (best is null || match.Score > best))
                best = match.Score;
        }
        return best;
    }
}
