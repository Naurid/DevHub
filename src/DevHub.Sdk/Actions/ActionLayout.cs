namespace DevHub.Actions;

/// <summary>
/// Arranges actions by one of their order properties (<see cref="AppAction.MenuOrder"/>,
/// <see cref="AppAction.ToolbarOrder"/>, <see cref="AppAction.WelcomeOrder"/>): lowest first,
/// with a <c>null</c> (separator) wherever the order crosses into a new hundred. Ties keep
/// registration order (plugins register in dependency order, so that's stable).
/// </summary>
public static class ActionLayout
{
    /// <param name="order">The order to use; actions where it's null are left out.</param>
    public static IReadOnlyList<AppAction?> Arrange(IEnumerable<AppAction> actions, Func<AppAction, int?> order)
    {
        var placed = actions
            .Select((action, index) => (Action: action, Order: order(action), Index: index))
            .Where(x => x.Order is not null)
            .OrderBy(x => x.Order!.Value)
            .ThenBy(x => x.Index)
            .ToList();

        var result = new List<AppAction?>();
        int? group = null;
        foreach (var (action, value, _) in placed)
        {
            var thisGroup = (int)Math.Floor(value!.Value / 100.0);
            if (group is not null && thisGroup != group)
                result.Add(null);
            group = thisGroup;
            result.Add(action);
        }
        return result;
    }
}
