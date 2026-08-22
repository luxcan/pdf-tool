namespace PdfTool.App.Behaviors;

/// <summary>
/// A request to move one item of a list to another position, as the parameter of the command
/// <see cref="DragReorder"/> invokes. Indices are into the list as it stands when the drag reaches
/// that position, so a drag across several tiles arrives as a run of single moves.
/// </summary>
internal sealed record ReorderRequest(int From, int To);
