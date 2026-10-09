using System.Collections.ObjectModel;
using DevHub.Core.Projects;
using DevHub.Core.Settings;
using DevHub.Services;
using DevHub.Controls;

namespace DevHub.ViewModels.Pages;

/// <summary>
/// Settings page. For now it hosts the "Open Tools" map: which tool opens each language. It
/// persists only *overrides* — a row left at its policy default is removed from the saved file.
/// </summary>
public class SettingsViewModel : PageViewModelBase
{
    private readonly ISettingsStore _store;
    private readonly IReadOnlyList<DevTool> _tools;
    private readonly IReadOnlyList<string> _availableIds;
    private readonly AppSettings _settings;
    private readonly bool _loaded;

    public override string Title => "Settings";
    public override string Icon => IconKind.Settings;

    public ObservableCollection<LanguageToolRowViewModel> LanguageTools { get; } = new();

    public SettingsViewModel(ISettingsStore store, IToolLocator tools, IDialogService dialogs)
    {
        _store = store;
        _tools = tools.GetAvailableTools();
        _availableIds = _tools.Select(t => t.Id).ToList();
        _settings = _store.Load();

        foreach (var language in KnownLanguages.All)
        {
            _settings.LanguageTools.TryGetValue(language.Id, out var choice);
            LanguageTools.Add(new LanguageToolRowViewModel(
                language, _tools, _availableIds, choice, dialogs, OnRowChanged));
        }

        _loaded = true;
    }

    private void OnRowChanged()
    {
        if (!_loaded)
            return;

        foreach (var row in LanguageTools)
        {
            var choice = row.ToChoice();
            var isPlainDefault =
                choice.ToolId == DefaultToolPolicy.DefaultFor(row.LanguageId, _availableIds)
                && choice.ToolId != ToolIds.Custom;

            if (isPlainDefault)
                _settings.LanguageTools.Remove(row.LanguageId); // keep the file to overrides only
            else
                _settings.LanguageTools[row.LanguageId] = choice;
        }

        _store.Save(_settings);
    }
}
