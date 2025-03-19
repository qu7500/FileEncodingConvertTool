using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FileEncodingConvertTool.ViewModels;
using MainWindowViewModel = FileEncodingConvertTool.ViewModels.MainWindowViewModel;

namespace FileEncodingConvertTool.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        
    }
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Save();
        }
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new FileEncodingConvertTool.Views.AppsettingWindow
        {
            DataContext = new AppsettingWindowViewModel()
        };
        dialog.ShowDialog(this);
    }
}