using System.Windows;
using System.Windows.Controls;

namespace PdfTool.App.Controls;

/// <summary>
/// The file list shared by both tabs. Reordering is only meaningful where output order follows the
/// list, so the Merge tab shows those buttons and the Compress tab does not.
/// </summary>
public partial class DocumentListView : UserControl
{
    public static readonly DependencyProperty ShowReorderButtonsProperty = DependencyProperty.Register(
        nameof(ShowReorderButtons),
        typeof(bool),
        typeof(DocumentListView),
        new PropertyMetadata(false));

    public static readonly DependencyProperty ToolbarContentProperty = DependencyProperty.Register(
        nameof(ToolbarContent),
        typeof(object),
        typeof(DocumentListView),
        new PropertyMetadata(null));

    public DocumentListView()
    {
        InitializeComponent();
    }

    public bool ShowReorderButtons
    {
        get => (bool)GetValue(ShowReorderButtonsProperty);
        set => SetValue(ShowReorderButtonsProperty, value);
    }

    /// <summary>
    /// The owning tab's primary action, shown at the right of the list's own toolbar. It is hosted
    /// here rather than above the control so the two tabs keep a single row of chrome, and so the
    /// action disappears along with the list when the page picker takes over.
    /// </summary>
    public object? ToolbarContent
    {
        get => GetValue(ToolbarContentProperty);
        set => SetValue(ToolbarContentProperty, value);
    }
}
