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
    public void An_assembly_brings_its_own_translations()
    {
        // Sdk.Core embeds i18n/fr.json.
        Assert.Equal("hier", RelativeTime.Format(DateTimeOffset.Now.AddDays(-1).AddHours(-1), DateTimeOffset.Now));
        Assert.Equal("Barres empilées", L.T("Stacked bars"));
    }
}
