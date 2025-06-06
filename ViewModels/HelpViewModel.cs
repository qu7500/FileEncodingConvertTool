using FileEncodingChecker.ViewModels;
using ReactiveUI;

namespace FileEncodingConvertTool.ViewModels;

public class HelpViewModel : ViewModelBase
{
    private string? _title; // Declare a private string variable for the title of the view

    public HelpViewModel(int? id = null)
    {
        Title = $"Welcome to File Encoding Checker!{id}"; // Set the title of the view
        Name = id.ToString(); // Set the name of the view
        IsActive = id % 2 == 0; //  true;
    }

    public string? Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    } // Set the title of the view

    public bool IsActive { get; set; }
    public string? Name { get; }
}