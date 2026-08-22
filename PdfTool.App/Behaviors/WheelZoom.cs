using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PdfTool.App.Behaviors;

/// <summary>
/// Turns Ctrl and the wheel over a list into a request to zoom, reported as a command so the sizes
/// on offer stay the view model's to decide.
///
/// Ctrl is required rather than optional: the page picker is a scrolling list of a document's pages,
/// and a bare wheel has to keep scrolling it. Holding Ctrl to zoom is what every viewer this sits
/// beside already does.
/// </summary>
internal static class WheelZoom
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command",
        typeof(ICommand),
        typeof(WheelZoom),
        new PropertyMetadata(null, OnCommandChanged));

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox list)
            return;

        list.PreviewMouseWheel -= OnWheel;

        if (e.NewValue is not ICommand)
            return;

        list.PreviewMouseWheel += OnWheel;
    }

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        // Exactly Ctrl: Ctrl+Shift and the rest belong to whatever else may want them later.
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        var direction = Math.Sign(e.Delta);

        if (direction != 0 &&
            GetCommand((ListBox)sender) is { } command &&
            command.CanExecute(direction))
        {
            command.Execute(direction);
        }

        // Whether or not the zoom moved, the wheel has been spent: left to bubble it would scroll
        // the list at the same time, which is the one thing a zoom must not also do.
        e.Handled = true;
    }
}
