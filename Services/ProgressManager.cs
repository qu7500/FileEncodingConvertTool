using System;
using System.IO;
using System.Text.Json;
using System.Threading;
namespace FileEncodingChecker.Services
{
    public  class ProgressChangedEventArgs : EventArgs
    {
        public double MainPercentage { get; set; }
        public double SubPercentage { get; set; }
        public long TotalItems { get; set; }
        public long ProcessedItems { get; set; }
        public string? MainStatus { get; set; }
        public string? SubStatus { get; set; }
        public bool IsCancelled { get; set; }
        public double ItemsPerSecond { get; set; }
        public Exception? Error { get; set; }
    }
    public class ProgressManager : IDisposable
    {
        protected readonly CancellationTokenSource Cts = new();
        protected ProgressLevel _progress = new("进度");
        protected long _totalItems;
        protected long _processedItems;
        protected DateTime _startTime;
        protected Exception _lastError;
        protected ProgressChangedEventArgs? ProgressChangedEventArgs;
        public CancellationToken Token => Cts.Token;
        public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;
        public void InitializeProgress(int totalSteps, string prefix = "进度")
        {
            _progress = new ProgressLevel(prefix, totalSteps);
            _startTime = DateTime.Now;
        }
        public void InitializeItemCounter(long totalItems)
        {
            _totalItems = totalItems;
            _processedItems = 0;
        }
        //更新状态
        public void UpdateStatus(string status)
        {
            FireProgressEvent(status);
        }
        //更新进度
        public void UpdateProgress(int currentStep, string message = "")
        {
            _progress.Update(currentStep);
            FireProgressEvent(message);
        }
        public void IncrementItemCounter(int count = 1, string message = "")
        {
            Interlocked.Add(ref _processedItems, count);
            FireProgressEvent(message);
        }
        public void Cancel()
        {
            if (Cts.IsCancellationRequested) return;
            Cts.Cancel();
            FireProgressEvent("操作已取消", false);
        }
        public void LogError(Exception ex)
        {
            _lastError = ex;
            FireProgressEvent($"错误: {ex.Message}", false);
        }
        public void SaveProgress(string filePath)
        {
            var state = new
            {
                MainProgress = _progress,
                TotalItems = _totalItems,
                ProcessedItems = _processedItems,
                StartTime = _startTime
            };
            File.WriteAllText(filePath, JsonSerializer.Serialize(state));
        }
        public void LoadProgress(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("进度文件未找到");
            var json = File.ReadAllText(filePath);
            var state = JsonSerializer.Deserialize<dynamic>(json);
            _progress = new ProgressLevel(
                (string)state.MainProgress.Prefix, 
                (int)state.MainProgress.TotalSteps);
            _progress.Update((int)state.MainProgress.CurrentStep);
            _totalItems = (long)state.TotalItems;
            _processedItems = (long)state.ProcessedItems;
            _startTime = DateTime.Parse(state.StartTime.ToString());
        }
        protected virtual void FireProgressEvent(string message, bool isCancelled = false)
        {
            var elapsedTime = DateTime.Now - _startTime;
            Invoke(new ProgressChangedEventArgs
            {
                MainPercentage = _progress.Percentage,
                TotalItems = _totalItems,
                ProcessedItems = _processedItems,
                MainStatus = $"{_progress.GetStatus()} {message}".Trim(),
                IsCancelled = isCancelled,
                ItemsPerSecond = elapsedTime.TotalSeconds > 0 ? 
                    _processedItems / elapsedTime.TotalSeconds : 0,
                Error = _lastError
            });
        }
        protected void Invoke(ProgressChangedEventArgs args)
        {
            ProgressChanged?.Invoke(this, args);
        }
        public void Dispose()
        {
            Cts.Dispose();
            GC.SuppressFinalize(this);
        }
        protected class ProgressLevel(string prefix, int totalSteps = 1)
        {
            private string Prefix { get; } = prefix;
            private int TotalSteps { get; } = Math.Max(1, totalSteps);
            private int CurrentStep { get; set; }
            public double Percentage => Math.Round((double)CurrentStep / TotalSteps * 100, 2);
            public void Update(int currentStep) => 
                CurrentStep = Math.Clamp(currentStep, 0, TotalSteps);
            public string GetStatus() => $"{Prefix} ({CurrentStep}/{TotalSteps})";
        }
    }
    public class MultiProgressManager : ProgressManager
    {
        private ProgressLevel _subProgress = new("子进度");
        public void InitializeSubProgress(int totalSteps, string prefix = "子进度")
        {
            _subProgress = new ProgressLevel(prefix, totalSteps);
        }
        public void UpdateSubProgress(int currentStep, string message = "")
        {
            _subProgress.Update(currentStep);
            FireProgressEvent(message);
        }
        protected override void FireProgressEvent(string message, bool isCancelled = false)
        {
            var elapsedTime = DateTime.Now - _startTime;
            Invoke(new ProgressChangedEventArgs
            {
                MainPercentage = _progress.Percentage,
                SubPercentage = _subProgress.Percentage,
                TotalItems = _totalItems,
                ProcessedItems = _processedItems,
                MainStatus = _progress.GetStatus(),
                SubStatus = $"{_subProgress.GetStatus()} {message}".Trim(),
                IsCancelled = isCancelled,
                ItemsPerSecond = elapsedTime.TotalSeconds > 0 ? 
                    _processedItems / elapsedTime.TotalSeconds : 0,
                Error = _lastError
            });
        }
    }
}
