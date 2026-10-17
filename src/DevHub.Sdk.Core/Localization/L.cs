using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace DevHub.Sdk.Core.Localization;

/// <summary>
/// The text the app shows, in the user's language. The English text is the key: <c>L.T("Apply")</c>, and with values
/// <c>L.T($"{count} issues moved to {name}")</c>, looked up as "{0} issues moved to {1}". Text without a translation
/// stays English, so a plugin without a French file still works.
/// <para>
/// Translations come from every loaded assembly (the shell, the Sdk, each plugin and its Core): an embedded resource
/// whose name ends in <c>i18n.fr.json</c>, a JSON object of English → French. In a project:
/// <c>&lt;EmbeddedResource Include="i18n\fr.json" /&gt;</c>. XAML uses <c>{l:T 'Apply'}</c> (DevHub.Sdk).
/// </para>
/// The language is chosen once at startup (<see cref="Use"/>); changing it in Settings applies after a restart.
/// </summary>
public static class L
{
    public const string English = "en";
    public const string French = "fr";
    public const string System = "system";

    /// <summary>The languages the app has, by code, with their own name ("Français").</summary>
    public static IReadOnlyList<(string Code, string Name)> Languages { get; } = [(English, "English"), (French, "Français")];

    private static readonly ConcurrentDictionary<string, string> Strings = new(StringComparer.Ordinal);
    private static readonly HashSet<string> LoadedAssemblies = new(StringComparer.Ordinal);
    private static readonly Lock Gate = new();

    /// <summary>The language in use ("en" or "fr").</summary>
    public static string Language { get; private set; } = English;

    public static bool IsFrench => Language == French;

    /// <summary>
    /// Switches to a language ("en", "fr", or "system" / null = Windows' display language when the app has it) and sets
    /// the culture (dates, numbers) to match. Called once, before any window: text already shown doesn't change.
    /// </summary>
    public static void Use(string? language)
    {
        var code = Resolve(language);
        lock (Gate)
        {
            Language = code;
            Strings.Clear();
            LoadedAssemblies.Clear();
            if (code != English)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;   // plugins load later; for the app's lifetime
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    Load(assembly);
            }
        }
        if (code == French)
        {
            // Dates and numbers the French way too (17/10/2026, 1,5). In English the Windows region stays.
            var culture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        }
    }

    /// <summary>"system" or null → the Windows display language when the app has it, else English.</summary>
    public static string Resolve(string? language) => language switch
    {
        English or French => language,
        _ => CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == French ? French : English,
    };

    /// <summary>The translation of <paramref name="english"/>, or the English text when there's none.</summary>
    public static string T(string english) =>
        Language == English || english.Length == 0 ? english : Strings.TryGetValue(english, out var text) ? text : english;

    /// <summary>
    /// The translation of an interpolated text: <c>L.T($"{n} issues")</c> is looked up as "{0} issues", then filled in
    /// (with the format and alignment each value had).
    /// </summary>
    public static string T(ref Text text) => text.Format();

    /// <summary>The translation of a format ("{0} issues"), filled in with <paramref name="values"/>.</summary>
    public static string F(string englishFormat, params object?[] values) =>
        string.Format(CultureInfo.CurrentCulture, T(englishFormat), values);

    /// <summary>Adds translations (English → this language); later ones win. For tests and tools.</summary>
    public static void Add(IEnumerable<KeyValuePair<string, string>> translations)
    {
        foreach (var (english, text) in translations)
            if (text.Length > 0)
                Strings[english] = text;
    }

    private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs e)
    {
        lock (Gate)
            Load(e.LoadedAssembly);
    }

    private static void Load(Assembly assembly)
    {
        if (assembly.IsDynamic || assembly.FullName is not { } name || !LoadedAssemblies.Add(name))
            return;
        string[] resources;
        try
        {
            resources = assembly.GetManifestResourceNames();
        }
        catch (Exception e) when (e is NotSupportedException or FileNotFoundException)
        {
            return;
        }
        foreach (var resource in resources.Where(r => r.EndsWith($"i18n.{Language}.json", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resource);
                if (stream is not null && JsonSerializer.Deserialize<Dictionary<string, string>>(stream) is { } translations)
                    Add(translations);
            }
            catch (JsonException)
            {
                // A broken translation file leaves that assembly's text in English rather than stopping the app.
            }
        }
    }

    /// <summary>
    /// Collects an interpolated text as a format ("{0} issues moved to {1}") and its values, so the format can be
    /// translated before the values are put in.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct Text
    {
        private readonly StringBuilder _format;
        private readonly List<object?> _values;

        public Text(int literalLength, int formattedCount)
        {
            _format = new StringBuilder(literalLength + formattedCount * 4);
            _values = new List<object?>(formattedCount);
        }

        public void AppendLiteral(string literal) => _format.Append(literal.Replace("{", "{{").Replace("}", "}}"));

        public void AppendFormatted<TValue>(TValue value) => AppendFormatted(value, 0, null);

        public void AppendFormatted<TValue>(TValue value, string? format) => AppendFormatted(value, 0, format);

        public void AppendFormatted<TValue>(TValue value, int alignment) => AppendFormatted(value, alignment, null);

        public void AppendFormatted<TValue>(TValue value, int alignment, string? format)
        {
            _format.Append('{').Append(_values.Count.ToString(CultureInfo.InvariantCulture));
            if (alignment != 0)
                _format.Append(',').Append(alignment.ToString(CultureInfo.InvariantCulture));
            if (format is not null)
                _format.Append(':').Append(format);
            _format.Append('}');
            _values.Add(value);
        }

        internal readonly string Format() => string.Format(CultureInfo.CurrentCulture, T(_format.ToString()), [.. _values]);
    }
}
