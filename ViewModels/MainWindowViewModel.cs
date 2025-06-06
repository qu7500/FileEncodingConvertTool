using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Media;
using DynamicData;
using FileEncodingChecker.Services;
using FileEncodingChecker.Utils;
using FileEncodingChecker.ViewModels;
using FileEncodingConvertTool.Models;
using FileEncodingConvertTool.Utils;
using ReactiveUI;
using ProgressChangedEventArgs = FileEncodingChecker.Services.ProgressChangedEventArgs;

namespace FileEncodingConvertTool.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string HistoryPath = "historyPath.json";
    private IDisposable? _fitterDisposable;
    private IDisposable? _serchFileDisposable;
    private bool _isSelectedSubPath;

    public bool IsSelectedSubPath
    {
        get => _isSelectedSubPath;
        set => this.RaiseAndSetIfChanged(ref _isSelectedSubPath, value);
    }

    private string? _selectedPath;

    public string? SelectedPath
    {
        get => _selectedPath;
        set => this.RaiseAndSetIfChanged(ref _selectedPath, value);
    }

    private string? _targetEncoding;

    public string? TargetEncoding
    {
        get => _targetEncoding;
        set => this.RaiseAndSetIfChanged(ref _targetEncoding, value);
    }

    private bool _cancellation;

    public bool Cancellation
    {
        get => _cancellation;
        set => this.RaiseAndSetIfChanged(ref _cancellation, value);
    }
    
    private bool _isEnhancedMode;
    public bool IsEnhancedMode
    {
        get => _isEnhancedMode;

        set { this.RaiseAndSetIfChanged(ref _isEnhancedMode, value); EncodingDetectorUtil.IsEnhancedMode = value; }
    }

    private string? _stateInfo;

    public string? StateInfo
    {
        get => _stateInfo;
        set => this.RaiseAndSetIfChanged(ref _stateInfo, value);
    }

    private int _progressValue;

    public int ProgressValue
    {
        get => _progressValue;
        set => this.RaiseAndSetIfChanged(ref _progressValue, value);
    }

    private bool _progressVisible;

    public bool ProgressVisible
    {
        get => _progressVisible;
        set => this.RaiseAndSetIfChanged(ref _progressVisible, value);
    }

    private string? _validateExtension;

    public string? ValidateExtension
    {
        get => _validateExtension;
        set => this.RaiseAndSetIfChanged(ref _validateExtension, value);
    }

    private bool? _isSelectAll;

    public bool? IsSelectAll
    {
        get => _isSelectAll;
        set
        {
            this.RaiseAndSetIfChanged(ref _isSelectAll, value);
            if (value == null) return;
            var state = value.Value;
            foreach (var fileEncodingData in FileEncodingDatas)
            {
                fileEncodingData.IsChecked = state;
            }
        }
    }

    public ObservableCollection<string>? Paths { get; set; } = [];

    private ReadOnlyObservableCollection<FileEncodingData>? _fileEncodingDataDatas;

    public ReadOnlyObservableCollection<FileEncodingData>? FileEncodingDatas
    {
        get => _fileEncodingDataDatas;
        set => this.RaiseAndSetIfChanged(ref _fileEncodingDataDatas, value);
    } //筛选数据

    private readonly SourceList<FileEncodingData> _fileEncodingsSource = new(); //元数据
    public ObservableCollection<string> Encodings { get; set; } = [];
    public ObservableCollection<string>? FitterEncodings { get; set; } = [];

    private Window? _window;
    
    public void SetWindow(Window window)
    {
        _window = window;
    }
    
    private IBrush _currentBackground = Brush.Parse("#FFFFFF");
    public IBrush CurrentBackground
    {
        get => _currentBackground;
        set => this.RaiseAndSetIfChanged(ref _currentBackground, value);
    }

    private string _currentTheme = "light";
    public string CurrentTheme
    {
        get => _currentTheme;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentTheme, value);
            CurrentBackground = value == "light" ? Brush.Parse("#FFFFFF") : Brush.Parse("#1E1E1E");
        }
    }

    public MainWindowViewModel()
    {
        LoadFilePathlist();
        InitEncoding();
        InitFileEncodingDatas();
        
        ToggleThemeCommand = ReactiveCommand.Create(() => 
        {
            CurrentTheme = CurrentTheme == "light" ? "dark" : "light";
        });
        SelectPathCommand = ReactiveCommand.CreateFromTask(SelectPath);
        LoadSelectedPathCommand = ReactiveCommand.Create(DoSerchFile);
        DoConvertCommand = ReactiveCommand.CreateFromTask(DoConvert);
        CancelTokenCommand = ReactiveCommand.Create(CancelToken);
        OpenWindowCommand = ReactiveCommand.CreateFromTask(OpenWindow);
        ClearCommand = ReactiveCommand.Create(Clear);
        DoFitterEncodingCommand = ReactiveCommand.Create(DoFitterEncoding);
        this.WhenAnyValue(x => x.FitterEncodings.Count)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(void (_) => { DoFitterEncoding(); });
    }

    private Task OpenWindow()
    {
        throw new NotImplementedException();
    }

    private void InitEncoding()
    {
        var validCharsets = GetSupportedCharsets();
        foreach (var validCharset in validCharsets)
        {
            try
            {
                // add only those charsets which are supported by .NET
                Encoding encoding = Encoding.GetEncoding(validCharset);
                Encodings.Add(encoding.WebName);
                // add UTF-8/16 with BOM, right after UTF-8/16
                if (MyRegex().IsMatch(encoding.WebName))
                {
                    Encodings.Add(encoding.WebName + "-bom");
                }
            }
            catch
            {
                // ignored
            }
        }
        Encodings.Insert(0, "Unknown");
        TargetEncoding = Encodings.FirstOrDefault();
    }

    private void InitFileEncodingDatas()
    {
        _fitterDisposable = _fileEncodingsSource.Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out var fileEncodings)
            .Subscribe();
        FileEncodingDatas = fileEncodings;
    }
    private void UpdateProgress(object? sender, ProgressChangedEventArgs e)
    {
        ProgressVisible = true;
        StateInfo = e.MainStatus;
        ProgressValue = (int)e.MainPercentage;
    }

    private ProgressManager? _progressmanager;

    private async Task<List<string>?> CollectSelectedFiles(string? path, ProgressManager progressManager)
    {
        try
        {
            if (path == null) return null;
            List<string>? validate = null;
            if (ValidateExtension != null)
            {
                validate = Regex.Split(ValidateExtension, @"[\r\n]+").Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            return await Task.Run(() =>
            {
                progressManager.Token.ThrowIfCancellationRequested();
                var files = LoadFiles(path, validate, IsSelectedSubPath, progressManager);
                return files;
            }, progressManager.Token);
        }
        catch
        {
            return null;
        }
    }

    private List<string> LoadFiles(string path, List<string>? validCharsets, bool isRecursion = true,
        ProgressManager? progressManager = null)
    {
        progressManager?.UpdateStatus($"开始收集文件...: ");
        var files = new List<string>();
        if (!Directory.Exists(path)) return files;
        var directories = new List<string> { path };
        if (isRecursion)
        {
            directories.AddRange(SafeGetDirectories(path));
        }

        progressManager?.InitializeProgress(directories.Count);
        var index = 0;
        foreach (var directory in directories)
        {
            progressManager?.Token.ThrowIfCancellationRequested();
            index++;
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    progressManager?.Token.ThrowIfCancellationRequested();
                    if (validCharsets == null || validCharsets.Count == 0)
                    {
                        files.Add(file);
                    }
                    else if (validCharsets.Contains(Path.GetExtension(file)))
                    {
                        files.Add(file);
                    }
                }

                progressManager?.UpdateProgress(index, $"收集文件中...:{Path.GetFileName(directory)}");
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        progressManager?.UpdateStatus($"共收集到文件: {files.Count}");
        return files;
    }

    private List<string> SafeGetDirectories(string path)
    {
        var options = new EnumerationOptions
            { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.System | FileAttributes.Encrypted };
        return Directory.EnumerateDirectories(path, "*", options).ToList();
    }

    private readonly Lock _lock = new();

    private async Task InitFileData(List<string> files, ProgressManager? progressManager = null)
    {
        try
        {
            if (files.Count == 0) return;
            var semaphore = new SemaphoreSlim(20);
            var token = progressManager.Token;
            progressManager?.InitializeProgress(files.Count);
            progressManager?.IncrementItemCounter();
            var currentIndex = 0;
            for (var index = 0; index < files.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var file = files[index];
                await semaphore.WaitAsync(token);
                await Task.Run(() =>
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        var fileinfo = new FileEncodingData(file);
                        _fileEncodingsSource.Add(fileinfo);
                        lock (_lock)
                        {
                            currentIndex++;
                        }
                    }catch (Exception e)
                    {
                        // ignored
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    progressManager?.UpdateProgress(currentIndex, $"检测文件编码: {Path.GetFileName(file)}");
                    return Task.CompletedTask;
                }, token);
            }
            // await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
           
        }
    }

    private void ResetProgress(bool normal = false)
    {
        ProgressValue = 0;
        ProgressVisible = normal;
        // if (!normal)
        // StateInfo = string.Empty;
    }

    private static IEnumerable<string> GetSupportedCharsets()
    {
        var assembly = typeof(UtfUnknown.Core.BitPackage).Assembly;
        var codepageName = assembly.GetType("UtfUnknown.Core.CodepageName");
        if (codepageName == null) yield break;
        var charsetConstants = codepageName.GetFields(bindingAttr: BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var charsetConstant in charsetConstants)
        {
            if (charsetConstant.FieldType == typeof(string))
                yield return (string)charsetConstant.GetValue(null)!;
        }
    }

    [GeneratedRegex("^utf-16BE|utf-16|utf-8$")]
    private static partial Regex MyRegex();

    private void LoadFilePathlist()
    {
        var path = Path.Combine(Environment.CurrentDirectory, HistoryPath);
        Paths = JsonPersister.Load<ObservableCollection<string>>(path);
        if (Paths != null) SelectedPath = Paths.FirstOrDefault();
    }

    public void Save()
    {
        var path = Path.Combine(Environment.CurrentDirectory, HistoryPath);
        JsonPersister.Save(path, Paths);
    }

    #region ReactiveCommand

    public ICommand SelectPathCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand LoadSelectedPathCommand { get; }
    public ICommand DoConvertCommand { get; }
    public ICommand CancelTokenCommand { get; }
    public ICommand OpenWindowCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand DoFitterEncodingCommand { get; }

    private void DoFitterEncoding()
    {
        _fitterDisposable?.Dispose();
        _fitterDisposable = _fileEncodingsSource.Connect()
            .DeferUntilLoaded()
            .Filter(item =>
            {
                if (FitterEncodings == null || FitterEncodings.Count == 0) return true;
                return item.EncodingType is not null && FitterEncodings.Contains(item.EncodingType);
            })
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out var collection)
            .Subscribe(_ => { }, ex => Console.WriteLine($"绑定失败: {ex}"));
        FileEncodingDatas = collection;
        FileEncodingDatas.Cast<INotifyPropertyChanged>().ToList().ForEach(item =>
        {
            item.PropertyChanged -= FileEncodingDataPropertyChanged;
            item.PropertyChanged += FileEncodingDataPropertyChanged;
        });
    }

    private void FileEncodingDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Task.Delay(200);
        if (e.PropertyName != nameof(FileEncodingData.IsChecked)) return;
        if (FileEncodingDatas == null) return;
        var count = FileEncodingDatas.Count(item => item.IsChecked);
        if (count == 0)
        {
            IsSelectAll = false;
        }else if (count == FileEncodingDatas.Count)
        {
            IsSelectAll = true;
        }else
        {
            IsSelectAll = null;
        }
    }

    private async Task DoConvert()
    {
        var index = 0;
        var successCount = 0;
        if (FileEncodingDatas==null || FileEncodingDatas.Count == 0) return;
        if (string.IsNullOrEmpty(TargetEncoding) || TargetEncoding == "Unknown")
        {
            StateInfo = "请选择目标编码";
            return;
        }
        try
        {
            var lockerObject = new object();
            var selectedfiles = FileEncodingDatas.Where(item => item.IsChecked).ToList();
            if (selectedfiles.Count == 0 ) return; //Todo 增加提示框
            Cancellation = true;
            ResetProgress(true);
            _progressmanager = new MultiProgressManager();
            _progressmanager.ProgressChanged += UpdateProgress;
            _progressmanager.InitializeProgress(selectedfiles.Count);
            var token = _progressmanager.Token;
            var semaphore = new SemaphoreSlim(10);
            var tasks = new List<Task>();

            foreach (var item in selectedfiles)
            {
                try
                {
                    if (!Cancellation) break;
                    token.ThrowIfCancellationRequested();
                    await semaphore.WaitAsync(token);
                    var item1 = item;
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            token.ThrowIfCancellationRequested();
                            if (item1 is { FilePath: not null, EncodingType: not null })
                            {
                                var result = FileEncodingConverter.ConvertFileEncoding(item1.FilePath,
                                    item1.EncodingType,
                                    item1.FilePath,
                                    TargetEncoding, token, _progressmanager);
                                if (result != null)
                                {
                                    item1.EncodingType = result;
                                    lock (lockerObject)
                                    {
                                        successCount++;
                                    }
                                }
                            }

                            lock (lockerObject)
                            {
                                index++;
                            }

                            _progressmanager.UpdateProgress(index, $"转换文件: {Path.GetFileName(item1.FilePath)}");
                        }
                        catch (OperationCanceledException)
                        {
                            throw new OperationCanceledException("用户取消任务");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, token));
                }
                catch (Exception e)
                {
                    StateInfo = $" Error: {e.GetType()}  {e.Message}";
                }
            }

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine($" OperationCanceledException  {e.Message}");
        }
        catch (Exception e)
        {
            StateInfo = $" Error: {e.GetType()}  {e.Message}";
        }
        finally
        {
            _progressmanager?.Dispose();
            _progressmanager = null;
            StateInfo = ($"处理文件总数:{index}，转换文件成功数:{successCount}");
            ResetProgress();
            Cancellation = false;
        }
    }

    private void Clear()
    {
        Paths?.Clear();
        Save();
    }

    private void DoSerchFile()
    {
        _serchFileDisposable?.Dispose();
        _serchFileDisposable = this.WhenAnyValue(x => x.SelectedPath)
            .Throttle(TimeSpan.FromMilliseconds(400))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(LoadSelectedPath!);
    }

    private async void LoadSelectedPath(string filePath)
    {
        _fileEncodingsSource.Clear();
        try
        {
            Cancellation = true;
            _progressmanager = new ProgressManager();
            _progressmanager.ProgressChanged += UpdateProgress;
            var selectedfiles = await CollectSelectedFiles(filePath, _progressmanager);
            if (selectedfiles == null) return;
            if (selectedfiles.Count == 0)
            {
                StateInfo = "选择的路径中没有需要的文件";
                return;
            }

            await InitFileData(selectedfiles, _progressmanager);
            DoFitterEncoding();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Console.WriteLine($" Error: {e.GetType()}  {e.Message}");
        }
        finally
        {
            _serchFileDisposable?.Dispose();
            _progressmanager?.Dispose();
            _progressmanager = null;
            ResetProgress();
            Cancellation = false;
            StateInfo = $"共加载文件数:{_fileEncodingsSource.Count}";
        }
    }

    //重置状态栏
    private void CancelToken()
    {
        Cancellation = false; // 设置取消标志为false
        switch (_progressmanager?.Token)
        {
            case { IsCancellationRequested: true }:
            case null:
                return; // 确保任务尚未被取消
            default:
                _progressmanager?.Cancel(); // 请求取消任务
                break;
        }
    }

    private async Task SelectPath()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } provider)
            throw new NullReferenceException("Missing StorageProvider instance.");
        var files = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            Title = "选择文件路径",
            AllowMultiple = false
        });
        if (files.Count == 0) return;
        var path = files[0].Path.ToString().Replace(@"file:///", "");
        if (!Paths.Contains(path))
            Paths.Add(path);
        Dispatcher.UIThread.Post(() => { SelectedPath = path; });
    }

    #endregion
}
