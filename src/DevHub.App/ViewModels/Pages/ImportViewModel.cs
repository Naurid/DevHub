namespace DevHub.ViewModels.Pages;

public class ImportViewModel : PageViewModelBase
{
    public override string Title => "Import";
    public override string Icon => ((char)0xE8B5).ToString(); // Segoe MDL2: import

    public string Description =>
        "Turn an existing folder into a reusable template. (Coming in M7.)";
}
