using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfTool.App.Behaviors;

/// <summary>
/// Lets the items of a list box be dragged into a new order, reporting each move as a command
/// rather than editing the collection, so the list stays the view model's to own.
///
/// The move is applied as the pointer passes over a tile rather than when the button is released.
/// That makes the list itself the drag preview -- no adorner to keep aligned with a virtualising
/// panel -- and it settles rather than oscillates, because once a move lands the dragged item is
/// the one under the pointer and the next pass has nothing left to do.
/// </summary>
internal static class DragReorder
{
    /// <summary>Private to this application, so a drag from elsewhere is never mistaken for one of these.</summary>
    private const string ItemFormat = "PdfTool.DragReorder.Item";

    // A drag is a single, exclusive interaction, so the pending one needs no per-list bookkeeping.
    private static Point _origin;
    private static object? _candidate;
    private static ListBox? _candidateList;

    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command",
        typeof(ICommand),
        typeof(DragReorder),
        new PropertyMetadata(null, OnCommandChanged));

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox list)
            return;

        list.PreviewMouseLeftButtonDown -= OnButtonDown;
        list.PreviewMouseMove -= OnMouseMove;
        list.PreviewMouseLeftButtonUp -= OnButtonUp;
        list.MouseLeave -= OnMouseLeave;
        list.Unloaded -= OnUnloaded;
        list.DragOver -= OnDragOver;
        list.Drop -= OnDrop;

        if (e.NewValue is not ICommand)
            return;

        list.AllowDrop = true;
        list.PreviewMouseLeftButtonDown += OnButtonDown;
        list.PreviewMouseMove += OnMouseMove;
        list.PreviewMouseLeftButtonUp += OnButtonUp;
        list.MouseLeave += OnMouseLeave;
        list.Unloaded += OnUnloaded;
        list.DragOver += OnDragOver;
        list.Drop += OnDrop;
    }

    /// <summary>
    /// Forgets a press that never became a drag. Left armed, it would turn the next press anywhere
    /// on the window into a drag of whatever was last clicked here, and it would hold that page and
    /// its thumbnail alive for as long as the process runs.
    /// </summary>
    private static void Disarm()
    {
        _candidate = null;
        _candidateList = null;
    }

    private static void OnButtonUp(object sender, MouseButtonEventArgs e) => Disarm();

    private static void OnMouseLeave(object sender, MouseEventArgs e) => Disarm();

    private static void OnUnloaded(object sender, RoutedEventArgs e) => Disarm();

    private static void OnButtonDown(object sender, MouseButtonEventArgs e)
    {
        var list = (ListBox)sender;

        _origin = e.GetPosition(list);
        Disarm();

        // A press on the tick box is the user selecting a page, and turning that into a drag would
        // make the box impossible to click.
        if (FindAncestor<ToggleButton>(e.OriginalSource as DependencyObject) is not null)
            return;

        _candidate = ItemUnder(list, e.OriginalSource as DependencyObject);
        _candidateList = _candidate is null ? null : list;
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        var list = (ListBox)sender;

        // The press has to have happened in this list, or a button held down elsewhere would drag
        // whatever was last clicked here.
        if (_candidate is null || !ReferenceEquals(_candidateList, list))
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            Disarm();
            return;
        }

        var position = e.GetPosition(list);

        // Below the system's threshold this is a click that wobbled, not a drag.
        if (Math.Abs(position.X - _origin.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _origin.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var dragged = _candidate;
        Disarm();

        // Where it started, so an abandoned drag can be undone: the moves are applied on the way
        // past, and pressing Escape or dropping outside has to put the item back rather than leave
        // it wherever the pointer last hovered.
        var origin = list.Items.IndexOf(dragged);

        var effect = DragDrop.DoDragDrop(list, new DataObject(ItemFormat, dragged), DragDropEffects.Move);

        if (effect == DragDropEffects.None && origin >= 0)
            RequestMove(list, list.Items.IndexOf(dragged), origin);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        var list = (ListBox)sender;

        // Files dropped onto the window are somebody else's event; leave them to bubble.
        if (!e.Data.GetDataPresent(ItemFormat))
            return;

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var dragged = e.Data.GetData(ItemFormat);
        var target = ItemUnder(list, list.InputHitTest(e.GetPosition(list)) as DependencyObject);

        if (dragged is null || target is null || ReferenceEquals(dragged, target))
            return;

        RequestMove(list, list.Items.IndexOf(dragged), list.Items.IndexOf(target));
    }

    private static void RequestMove(ListBox list, int from, int to)
    {
        if (from < 0 || to < 0 || from == to)
            return;

        var request = new ReorderRequest(from, to);

        if (GetCommand(list) is { } command && command.CanExecute(request))
            command.Execute(request);
    }

    /// <summary>
    /// The move has already been applied on the way past, so the drop itself only has to stop the
    /// window treating it as a file being dropped in.
    /// </summary>
    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ItemFormat))
            e.Handled = true;
    }

    private static object? ItemUnder(ListBox list, DependencyObject? source)
    {
        if (FindAncestor<ListBoxItem>(source) is not { } container)
            return null;

        var item = list.ItemContainerGenerator.ItemFromContainer(container);
        return item == DependencyProperty.UnsetValue ? null : item;
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match)
                return match;

            // Template content hangs off the visual tree; run-of-the-mill content off the logical
            // one. Walking only the visual tree stops short of a ListBoxItem in some templates.
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }

        return null;
    }
}
