using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace ProgressToolkit;

public enum EventId
{
    UpdateStatus,
    UpdateProgress,
    IncrementItemCounter,
    Cancel,
    LogError,
    InitializeProgress,
    SaveProgress,
    LoadProgress
}

/// <summary>
///     进度变更事件参数
/// </summary>
public class ProgressChangedEventArgs : EventArgs
{
    public EventId EventId { get; init; }
    public double MainPercentage { get; set; }
    public long TotalItems { get; set; }
    public long ProcessedItems { get; set; }
    public string? MainStatus { get; set; }
    public bool IsCancelled { get; set; }
    public double ItemsPerSecond { get; set; }
    public Exception? Error { get; set; }
}

/// <summary>
///     进度管理器（线程安全）
/// </summary>
public class ProgressManager : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly object _syncRoot = new();
    private Exception? _lastError;
    private long _processedItems;
    private ProgressInfo _progress = new("进度");
    private DateTime _startTime;
    private long _totalItems;
    public Guid? Parent { get; set; }
    public CancellationToken Token => _cts.Token;
    public Guid Id { get; } = Guid.NewGuid();
    public string Alias { get; set; } = string.Empty;

    public void Dispose()
    {
        _cts.Dispose();
        ProgressChanged = null;
        GC.SuppressFinalize(this);
    }

    public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;

    /// <summary>
    ///     初始化多步骤进度
    /// </summary>
    public void InitializeProgress(int totalSteps, string prefix = "进度")
    {
        lock (_syncRoot)
        {
            _progress = new ProgressInfo(prefix, totalSteps);
            _startTime = DateTime.Now;
        }
    }

    /// <summary>
    ///     初始化项目计数器
    /// </summary>
    public void InitializeItemCounter(long totalItems)
    {
        Interlocked.Exchange(ref _totalItems, totalItems);
        Interlocked.Exchange(ref _processedItems, 0);
    }

    /// <summary>
    ///     更新状态文本
    /// </summary>
    public void UpdateStatus(string status)
    {
        var args = CreateProgressEventArgs(status, EventId.UpdateStatus);
        OnProgressChanged(this, args);
    }

    /// <summary>
    ///     更新步骤进度
    /// </summary>
    public void UpdateProgress(int currentStep, string message = "")
    {
        lock (_syncRoot)
        {
            _progress.Update(currentStep);
        }

        var args = CreateProgressEventArgs(message, EventId.UpdateProgress);
        OnProgressChanged(this, args);
    }

    /// <summary>
    ///     增加已处理项目计数
    /// </summary>
    public void IncrementItemCounter(int count = 1, string message = "")
    {
        Interlocked.Add(ref _processedItems, count);
        var args = CreateProgressEventArgs(message, EventId.IncrementItemCounter);
        OnProgressChanged(this, args);
    }

    /// <summary>
    ///     取消操作
    /// </summary>
    public void Cancel()
    {
        if (_cts.IsCancellationRequested) return;
        _cts.Cancel();
        var args = CreateProgressEventArgs("操作已取消", EventId.Cancel, true);
        OnProgressChanged(this, args);
        ProgressChanged = null; // 防止后续事件触发
    }

    /// <summary>
    ///     记录错误
    /// </summary>
    private void LogError(Exception ex)
    {
        _lastError = ex;
        var args = CreateProgressEventArgs($"错误: {ex.Message}", EventId.LogError);
        OnProgressChanged(this, args);
    }

    /// <summary>
    ///     保存进度到文件
    /// </summary>
    public void SaveProgress(string filePath)
    {
        try
        {
            var state = new
            {
                MainProgress = _progress,
                TotalItems = Interlocked.Read(ref _totalItems),
                ProcessedItems = Interlocked.Read(ref _processedItems),
                StartTime = _startTime
            };
            File.WriteAllText(filePath, JsonSerializer.Serialize(state));
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    /// <summary>
    ///     从文件加载进度
    /// </summary>
    public void LoadProgress(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("进度文件未找到");
        try
        {
            var json = File.ReadAllText(filePath);
            var state = JsonSerializer.Deserialize<dynamic>(json);
            lock (_syncRoot)
            {
                _progress = new ProgressInfo(
                    (string)state.MainProgress.Prefix,
                    (int)state.MainProgress.TotalSteps);
                _progress.Update((int)state.MainProgress.CurrentStep);
            }

            Interlocked.Exchange(ref _totalItems, (long)state.TotalItems);
            Interlocked.Exchange(ref _processedItems, (long)state.ProcessedItems);
            _startTime = DateTime.Parse(state.StartTime.ToString());
        }
        catch (Exception ex)
        {
            LogError(ex);
            throw;
        }
    }

    /// <summary>
    ///     根据事件类型动态构建进度事件参数
    /// </summary>
    private ProgressChangedEventArgs CreateProgressEventArgs(
        string message,
        EventId eventId,
        bool isCancelled = false)
    {
        var args = new ProgressChangedEventArgs
        {
            EventId = eventId,
            IsCancelled = isCancelled,
            Error = _lastError
        };
        // 根据事件类型填充特定数据
        switch (eventId)
        {
            case EventId.InitializeProgress:
                lock (_syncRoot)
                {
                    args.MainStatus = $"{_progress.Prefix} 初始化完成";
                    args.MainPercentage = 0;
                }

                break;
            case EventId.UpdateStatus:
                args.MainStatus = message;
                break;
            case EventId.UpdateProgress:
                lock (_syncRoot)
                {
                    args.MainPercentage = _progress.Percentage;
                    args.MainStatus = $"{_progress.GetStatus()} {message}".Trim();
                }

                break;
            case EventId.IncrementItemCounter:
                var processed = Interlocked.Read(ref _processedItems);
                var total = Interlocked.Read(ref _totalItems);
                var elapsed = DateTime.Now - _startTime;
                args.ProcessedItems = processed;
                args.TotalItems = total;
                args.ItemsPerSecond = elapsed.TotalSeconds > 0 ? processed / elapsed.TotalSeconds : 0;
                args.MainStatus = $"{processed}/{total} {message}".Trim();
                break;
            case EventId.LogError:
                args.MainStatus = $"错误: {message}";
                args.Error = _lastError;
                break;
            case EventId.SaveProgress:
            case EventId.LoadProgress:
                args.MainStatus = $"{eventId}: {message}";
                break;
            case EventId.Cancel:
                args.MainStatus = "操作已取消";
                args.IsCancelled = true;
                break;
            default:
                args.MainStatus = "未知操作类型";
                break;
        }

        // 公共数据（所有事件类型都需要）
        if (eventId == EventId.IncrementItemCounter) return args; // 已单独处理
        args.TotalItems = Interlocked.Read(ref _totalItems);
        args.ProcessedItems = Interlocked.Read(ref _processedItems);
        return args;
    }

    /// <summary>
    ///     处理子进度管理器的事件冒泡
    /// </summary>
    protected virtual void OnProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        if (sender is ProgressManager subPm && subPm.Parent == Id)
        {
            // 过滤子进度完成事件
            if (e.EventId != EventId.UpdateProgress || !(e.MainPercentage >= 100)) return;
            lock (_syncRoot)
            {
                _progress.AddStep();
            }

            var args = CreateProgressEventArgs("", EventId.UpdateProgress);
            ProgressChanged?.Invoke(this, args);
            return;
        }

        ProgressChanged?.Invoke(this, e);
    }

    /// <summary>
    ///     订阅子进度事件
    /// </summary>
    public void Subscribe(ProgressManager pm)
    {
        pm.ProgressChanged += OnProgressChanged;
    }
}

/// <summary>
///     进度信息（线程安全）
/// </summary>
public class ProgressInfo(string prefix, int totalSteps = 1)
{
    private readonly object _syncRoot = new();
    public string Prefix { get; } = prefix;
    private int TotalSteps { get; } = Math.Max(1, totalSteps);
    private int CurrentStep { get; set; }

    public double Percentage =>
        Math.Round((double)CurrentStep / TotalSteps * 100, 2);

    public void Update(int currentStep)
    {
        lock (_syncRoot)
        {
            CurrentStep = Math.Clamp(currentStep, 0, TotalSteps);
        }
    }

    public string GetStatus()
    {
        return $"{Prefix} ({CurrentStep}/{TotalSteps})";
    }

    public void AddStep()
    {
        lock (_syncRoot)
        {
            CurrentStep = Math.Clamp(CurrentStep + 1, 0, TotalSteps);
        }
    }
}

/// <summary>
///     进度管理器集合（支持层级结构）
/// </summary>
public class ProgressManagerCollection
{
    private readonly Dictionary<Guid, ProgressManager> _progressManagers = new();
    private readonly object _syncRoot = new();

    public ProgressManager CreateProgressManager(string prefix, Guid? parentId = null)
    {
        var pm = new ProgressManager
        {
            Alias = prefix,
            Parent = parentId
        };
        lock (_syncRoot)
        {
            if (parentId.HasValue && _progressManagers.TryGetValue(parentId.Value, out var parent))
                parent.Subscribe(pm);
            _progressManagers[pm.Id] = pm;
        }

        pm.ProgressChanged += OnProgressChanged;
        return pm;
    }

    public void RemoveProgressManager(Guid id)
    {
        lock (_syncRoot)
        {
            if (!_progressManagers.TryGetValue(id, out var pm)) return;
            pm.ProgressChanged -= OnProgressChanged;
            pm.Dispose();
            _progressManagers.Remove(id);
        }
    }

    public ProgressManager? GetProgressManager(Guid id)
    {
        lock (_syncRoot)
        {
            return _progressManagers.GetValueOrDefault(id);
        }
    }

    public ProgressManager? GetProgressManagerByAlias(string alias)
    {
        lock (_syncRoot)
        {
            return _progressManagers.Values.FirstOrDefault(pm => pm.Alias == alias);
        }
    }

    private void OnProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        if (sender is not ProgressManager senderPm) return;
        // 级联取消
        if (e.EventId == EventId.Cancel)
        {
            lock (_syncRoot)
            {
                foreach (var pm in _progressManagers.Values.Where(pm => pm.Parent == senderPm.Id)) pm.Cancel();
            }

            return;
        }

        ProgressChanged?.Invoke(sender, e);
    }

    public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;
}