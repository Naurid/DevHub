using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace DevHub.Controls;

/// <summary>
/// A horizontal row of <see cref="ActionButton"/>s (and <see cref="ToolbarSeparator"/>s),
/// evenly spaced by the Spacing.Toolbar token. Children go straight inside:
/// <c>&lt;c:Toolbar&gt;&lt;c:ActionButton … /&gt;&lt;c:ToolbarSeparator /&gt;…&lt;/c:Toolbar&gt;</c>.
/// Look: Themes/Components/Toolbar.axaml.
/// </summary>
public class Toolbar : ItemsControl
{
}

/// <summary>A thin vertical line between groups of toolbar buttons.</summary>
public class ToolbarSeparator : TemplatedControl
{
}
