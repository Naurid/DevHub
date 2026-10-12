using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DevHub.ViewModels.ToolWindows;

/// <summary>The test plugin's tool window: a counter, to see a plugin's bindings and commands work.</summary>
public sealed partial class TestToolWindowViewModel : ToolWindowViewModelBase
{
    public override string Id => "test-plugin";
    public override string Title => "Test Plugin";
    public override string IconKind => Controls.IconKind.Info;
    public override ToolWindowSlot Slot => ToolWindowSlot.Right;

    [ObservableProperty] private int _clicks;

    public string Text => $"Clicked {Clicks}×";

    partial void OnClicksChanged(int value) => OnPropertyChanged(nameof(Text));

    [RelayCommand]
    private void Click() => Clicks++;
}
