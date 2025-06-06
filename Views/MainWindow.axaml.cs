using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FileEncodingConvertTool.ViewModels;
using MainWindowViewModel = FileEncodingConvertTool.ViewModels.MainWindowViewModel;
using ReactiveUI;

namespace FileEncodingConvertTool.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainWindowViewModel();
        DataContext = vm;
        vm.SetWindow(this);
        
        // 初始主题设置
        Classes.Set("light", true);
        
        // 监听主题变化
        vm.WhenAnyValue(x => x.CurrentTheme)
            .Subscribe(theme => 
            {
                Classes.Set("light", theme == "light");
                Classes.Set("dark", theme == "dark");
            });
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
