using Avalonia.Input;

namespace DevHub.Actions;

/// <summary>
/// Parses shortcut strings like "Ctrl+Shift+K", "F5" or "Alt+1" into Avalonia KeyGestures.
/// Use this instead of <see cref="KeyGesture.Parse"/> directly.
/// </summary>
/// <remarks>
/// Avalonia 12 quirk: <c>KeyGesture.Parse("Alt+1")</c> falls back to
/// <c>Enum.TryParse&lt;Key&gt;("1")</c>, which reads "1" as the enum's numeric value
/// (Key.Cancel), not the digit key (Key.D1). The binding then never fires. Here a lone
/// digit is rewritten to "D1" first, so action definitions can keep the readable form.
/// </remarks>
public static class Gestures
{
    public static KeyGesture Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var lastPlus = text.LastIndexOf('+');
        var key = text[(lastPlus + 1)..];

        if (key.Length == 1 && char.IsAsciiDigit(key[0]))
            text = text[..(lastPlus + 1)] + "D" + key;

        return KeyGesture.Parse(text);
    }
}
