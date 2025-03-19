using System;
using System.IO;
using System.Text;
using FileEncodingChecker.Utils;
using FileEncodingChecker.ViewModels;
using FileEncodingConvertTool.Utils;
using ReactiveUI;

namespace FileEncodingConvertTool.Models;

public class FileEncodingData: ViewModelBase
{
    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }
    public string? FileName { get; set; }

    private string? _encodingType;

    public string? EncodingType
    {
        get => _encodingType;
        set => this.RaiseAndSetIfChanged(ref _encodingType, value);
    }
    public string? Extension { get; set; }
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public DateTime Created { get; set; }
    public string? FilePath { get; set; }

    public FileEncodingData(string path)
    {
        if (!File.Exists(path)) return;
        FileName = Path.GetFileNameWithoutExtension(path);
        Extension = Path.GetExtension(path);
        FilePath = path;
        Created = File.GetCreationTime(path);
        EncodingType =  GetEncodingName(path);
    }
    private string GetEncodingName(string path)
    {
        var encoding = EncodingDetectorUtil.SmartDetect(path, out var whithBom);
        if (encoding != null)
        {
            return encoding.WebName+ (whithBom ? "-bom" : ""); 
        }
        return "Unknown";
    }
}