namespace DevHub.Actions;

/// <summary>
/// All actions in the app, looked up by id. Built once from every registered
/// <see cref="IActionProvider"/>. Menus, toolbars, the keymap and Find Action all read
/// from here, so an action is defined in exactly one place.
/// </summary>
public sealed class ActionRegistry
{
    private readonly Dictionary<string, AppAction> _byId = new(StringComparer.Ordinal);
    private readonly List<AppAction> _all = [];

    public ActionRegistry(IEnumerable<IActionProvider> providers)
    {
        foreach (var provider in providers)
        {
            foreach (var action in provider.GetActions())
            {
                if (!_byId.TryAdd(action.Id, action))
                {
                    throw new InvalidOperationException(
                        $"Duplicate action id '{action.Id}' (from {provider.GetType().Name}).");
                }

                _all.Add(action);
            }
        }
    }

    /// <summary>Every action, in provider registration order.</summary>
    public IReadOnlyList<AppAction> All => _all;

    /// <summary>The action with this id; throws if there is none (a typo in an id is a bug).</summary>
    public AppAction Get(string id) =>
        _byId.TryGetValue(id, out var action)
            ? action
            : throw new KeyNotFoundException($"Unknown action id '{id}'. See ActionIds.");

    public bool TryGet(string id, out AppAction? action) => _byId.TryGetValue(id, out action);
}
