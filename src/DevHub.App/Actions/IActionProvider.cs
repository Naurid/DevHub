namespace DevHub.Actions;

/// <summary>
/// Contributes actions to the <see cref="ActionRegistry"/>. Each feature area has one
/// (view, repositories, later git…), registered in DI as
/// <c>services.AddSingleton&lt;IActionProvider, XyzActionProvider&gt;()</c>; the registry
/// receives all of them as <c>IEnumerable&lt;IActionProvider&gt;</c>.
/// </summary>
public interface IActionProvider
{
    IEnumerable<AppAction> GetActions();
}
