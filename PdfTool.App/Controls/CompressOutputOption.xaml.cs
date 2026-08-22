using System.Windows;
using System.Windows.Controls;

namespace PdfTool.App.Controls;

/// <summary>
/// Offers to shrink whatever the tab is about to write, at the quality the Compress tab is set to.
/// Merge and Split both end in files worth compressing, and an option that looked or behaved
/// differently between them would suggest the two do different things. They do not.
/// </summary>
public partial class CompressOutputOption : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(CompressOutputOption),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked),
        typeof(bool),
        typeof(CompressOutputOption),
        // Two-way by default, the same as the check box it stands for, so a tab can bind its own
        // setting without having to say so.
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public CompressOutputOption()
    {
        InitializeComponent();
    }

    /// <summary>Wording for the check box, naming what this tab produces.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Whether the tab should compress its output.</summary>
    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }
}
