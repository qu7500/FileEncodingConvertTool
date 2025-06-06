using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using FileEncodingChecker.ViewModels;
using ReactiveUI;

namespace FileEncodingConvertTool.ViewModels;

public class AppsettingWindowViewModel : ViewModelBase
{
    private readonly SourceList<HelpViewModel> _sourceList = new();
    private ViewModelBase? _selectedItem;

    public AppsettingWindowViewModel()
    {
        for (var i = 0; i < 9; i++) _sourceList.Add(new HelpViewModel(i));
        // 动态数据流：过滤、排序、绑定到 UI 集合
        _sourceList.Connect()
            .Filter(x => x.IsActive) // 过滤
            .Sort(SortExpressionComparer<HelpViewModel>.Ascending(x => x.Name ?? string.Empty)) // 排序
            .ObserveOn(RxApp.MainThreadScheduler) // 确保在 UI 线程更新
            .Bind(out var observableCollection) // 绑定到 ObservableCollection
            .Subscribe();
        Items = observableCollection;
    }

    // 公开一个只读的 ObservableCollection 供 UI 绑定
    public ReadOnlyObservableCollection<HelpViewModel> Items { get; }

    public ViewModelBase? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }
}