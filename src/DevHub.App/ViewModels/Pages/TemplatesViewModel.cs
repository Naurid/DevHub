using DevHub.Controls;

namespace DevHub.ViewModels.Pages;

public class TemplatesViewModel : PageViewModelBase
{
    public override string Title => "Templates";
    public override string Icon => IconKind.Template;

    public string Description =>
        "Browse your project and file templates. (Coming in M1 / M4.)";
}
