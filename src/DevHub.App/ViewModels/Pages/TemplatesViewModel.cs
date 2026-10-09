namespace DevHub.ViewModels.Pages;

public class TemplatesViewModel : PageViewModelBase
{
    public override string Title => "Templates";
    public override string Icon => ((char)0xE8A5).ToString(); // Segoe MDL2: document

    public string Description =>
        "Browse your project and file templates. (Coming in M1 / M4.)";
}
