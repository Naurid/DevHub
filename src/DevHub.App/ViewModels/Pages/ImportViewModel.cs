using DevHub.Controls;

namespace DevHub.ViewModels.Pages;

public class ImportViewModel : PageViewModelBase
{
    public override string Title => "Import";
    public override string Icon => IconKind.Import;

    public string Description =>
        "Turn an existing folder into a reusable template. (Coming in M7.)";
}
