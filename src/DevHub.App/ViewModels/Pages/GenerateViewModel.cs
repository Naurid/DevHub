namespace DevHub.ViewModels.Pages;

public class GenerateViewModel : PageViewModelBase
{
    public override string Title => "Generate";
    public override string Icon => ((char)0xE710).ToString(); // Segoe MDL2: add

    public string Description =>
        "Pick a template, fill in variables, and stamp out a new project or file. (Coming in M4.)";
}
