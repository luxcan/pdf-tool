using System.Windows;
using System.Windows.Input;

namespace PdfTool.App.Behaviors;

/// <summary>
/// Runs a command when an element is first laid out, passing its data context. Attached to the
/// page-thumbnail template so that a virtualising panel only triggers rendering for the items it
/// actually realises, rather than for every page in the document.
/// </summary>
internal static class RealizedItem
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command",
        typeof(ICommand),
        typeof(RealizedItem),
        new PropertyMetadata(null, OnCommandChanged));

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        element.Loaded -= OnRealized;
        element.DataContextChanged -= OnDataContextChanged;

        if (e.NewValue is not ICommand)
            return;

        // Loaded covers the first realisation; DataContextChanged covers container recycling,
        // where the same element is handed a different page instead of being recreated.
        element.Loaded += OnRealized;
        element.DataContextChanged += OnDataContextChanged;
    }

    private static void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        Invoke((FrameworkElement)sender);

    private static void OnRealized(object sender, RoutedEventArgs e) =>
        Invoke((FrameworkElement)sender);

    private static void Invoke(FrameworkElement element)
    {
        var command = GetCommand(element);

        if (element.DataContext is not null && command?.CanExecute(element.DataContext) == true)
            command.Execute(element.DataContext);
    }
}
