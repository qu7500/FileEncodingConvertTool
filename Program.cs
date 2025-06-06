using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;

namespace FileEncodingConvertTool;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("Starting application...");
            Console.WriteLine($"Command line args: {string.Join(" ", args)}");
            Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
            
            var app = BuildAvaloniaApp();
            
            Console.WriteLine("Configuring desktop lifetime...");
            var lifetime = new ClassicDesktopStyleApplicationLifetime()
            {
                Args = args,
                ShutdownMode = ShutdownMode.OnLastWindowClose
            };
            
            Console.WriteLine("Starting application setup...");
            app.SetupWithLifetime(lifetime);
            Console.WriteLine("Application setup completed");
            
            if (lifetime.MainWindow == null)
            {
                Console.WriteLine("Error: MainWindow is null after setup");
                Console.WriteLine("Available windows:");
                foreach (var window in lifetime.Windows)
                {
                    Console.WriteLine($"- {window?.Title ?? "null"}");
                }
                return;
            }
            
            Console.WriteLine($"MainWindow created: {lifetime.MainWindow.Title}");
            Console.WriteLine("Starting application lifetime...");
            lifetime.Start(args);
            
            Console.WriteLine("Application exited normally");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Application failed to start: {ex}");
            Console.WriteLine($"Exception details: {ex.ToString()}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // 写入Windows事件日志
            try
            {
                using (EventLog eventLog = new EventLog("Application"))
                {
                    eventLog.Source = "FileEncodingConvertTool";
                    eventLog.WriteEntry($"Application failed to start: {ex}", EventLogEntryType.Error);
                }
            }
            catch (Exception logEx)
            {
                Console.WriteLine($"Failed to write to event log: {logEx}");
            }
            
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        Console.WriteLine("Initializing Avalonia application...");
        
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(Avalonia.Logging.LogEventLevel.Verbose)
            .UseReactiveUI();

        Console.WriteLine("Avalonia application initialized successfully");
        Console.WriteLine($"Using Avalonia version: {typeof(AppBuilder).Assembly.GetName().Version}");
        Console.WriteLine($"Using .NET runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        
        return builder;
    }
}
