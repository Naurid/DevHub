namespace DevHub;

/// <summary>
/// The app's DI container, for the few places that can't get services injected: XAML markup
/// extensions such as <c>{a:Action …}</c>. The app sets it once at startup; it is null in the
/// XAML previewer. Everything else gets its services through its constructor.
/// </summary>
public static class AppServices
{
    public static IServiceProvider? Current { get; set; }
}
