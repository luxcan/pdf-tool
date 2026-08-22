using System.Windows;
using System.Windows.Threading;
using PdfTool.App;

namespace PdfTool.App.Tests;

/// <summary>
/// A single STA thread with a running dispatcher and one <see cref="Application"/>, shared by every
/// test. WPF permits only one Application per process, so this cannot be created per test.
/// Constructing it also loads App.xaml, which is what puts the theme's resources in scope.
/// </summary>
public sealed class WpfContext : IDisposable
{
    private readonly Thread _thread;
    private Dispatcher _dispatcher = null!;

    public WpfContext()
    {
        using var ready = new ManualResetEventSlim();

        _thread = new Thread(() =>
        {
            var application = new global::PdfTool.App.App { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            // WPF's generated entry point normally does this; it is what loads App.xaml and puts
            // the theme dictionary into Application.Current.Resources. OnStartup never runs because
            // Run() is never called, so no renderer or main window is created here.
            application.InitializeComponent();

            _dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(_dispatcher));

            // ReSharper disable once AccessToDisposedClosure - Wait() below happens before disposal.
            ready.Set();

            Dispatcher.Run();
        })
        {
            IsBackground = true
        };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait();
    }

    /// <summary>Runs the action on the UI thread and rethrows anything it threw.</summary>
    public void Invoke(Action action) => _dispatcher.Invoke(action);

    /// <summary>
    /// Runs asynchronous work on the UI thread and awaits it there, which is what a command bound to
    /// a button actually does. Unwrapping is what lets a failed assertion inside it reach the test.
    /// </summary>
    public Task InvokeAsync(Func<Task> work) => _dispatcher.InvokeAsync(work).Task.Unwrap();

    public void Dispose() => _dispatcher.InvokeShutdown();
}

[CollectionDefinition(Name)]
public sealed class WpfCollection : ICollectionFixture<WpfContext>
{
    public const string Name = "wpf";
}
