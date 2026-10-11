using CommunityToolkit.Mvvm.ComponentModel;

namespace DevHub.ViewModels;

/// <summary>
/// A picture shown by the <c>Avatar</c> control, filled in when the download finishes. One
/// instance can be shared (all of your commits point at the same one, so they update together).
/// </summary>
public sealed partial class AvatarViewModel : ObservableObject
{
    /// <summary>PNG / JPEG bytes, or null (the control shows the name's first letter).</summary>
    [ObservableProperty] private byte[]? _image;
}
