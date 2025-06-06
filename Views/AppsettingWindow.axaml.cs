using Avalonia.Controls;
using Avalonia.Interactivity;
using FileEncodingConvertTool.ViewModels;

namespace FileEncodingConvertTool.Views;

public partial class AppsettingWindow : Window
{
    public AppsettingWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        DataContext = new AppsettingWindowViewModel();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}