using DevHub.Controls;

namespace DevHub.ViewModels.Pages;

public class GenerateViewModel : PageViewModelBase
{
    public override string Title => "Generate";
    public override string Icon => IconKind.Add;

    public string Description =>
        "Pick a template, fill in variables, and stamp out a new project or file. (Coming in M4.)";
}
