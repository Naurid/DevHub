using Avalonia.Input;

namespace DevHub.Actions;

/// <summary>
/// Turns each action's <see cref="AppAction.Gesture"/> into a window-wide
/// <see cref="KeyBinding"/>. Applied once to the main window at startup.
/// </summary>
/// <remarks>
/// View-side helper (uses Avalonia input types), unlike AppAction itself.
/// User-customizable shortcuts arrive with the Settings dialog (UI-0 step 9): they will
/// change <see cref="AppAction.Gesture"/> and re-apply.
/// </remarks>
public static class Keymap
{
    /// <summary>
    /// Adds a KeyBinding for every action that has a shortcut. Throws if two actions share
    /// one, since only one of them could ever run.
    /// </summary>
    public static void Apply(ActionRegistry registry, InputElement target)
    {
        var owners = new Dictionary<KeyGesture, AppAction>();

        foreach (var action in registry.All)
        {
            if (string.IsNullOrWhiteSpace(action.Gesture))
                continue;

            var gesture = Gestures.Parse(action.Gesture);
            if (owners.TryGetValue(gesture, out var other))
            {
                throw new InvalidOperationException(
                    $"Shortcut '{action.Gesture}' is assigned to both '{other.Id}' and '{action.Id}'.");
            }

            owners[gesture] = action;
            target.KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = action.Command });
        }
    }
}
