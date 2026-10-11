using CommunityToolkit.Mvvm.ComponentModel;

namespace DevHub.ViewModels;

/// <summary>
/// Base for every ViewModel. <see cref="ObservableObject"/> provides
/// INotifyPropertyChanged so [ObservableProperty]-generated setters can raise change
/// notifications the bindings listen to. (Think: the data a view "reads", with change events.)
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
