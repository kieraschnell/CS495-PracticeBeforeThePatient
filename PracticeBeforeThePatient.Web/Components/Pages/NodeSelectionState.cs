using PracticeBeforeThePatient.Core.Models;

namespace PracticeBeforeThePatient.Web.Components.Pages;

public sealed class NodeSelectionState
{
    public Node? SelectedNode { get; private set; }

    public event Action? SelectionChanged;

    public void Select(Node node)
    {
        if (ReferenceEquals(SelectedNode, node))
        {
            return;
        }

        SelectedNode = node;
        SelectionChanged?.Invoke();
    }

    public void Clear()
    {
        if (SelectedNode is null)
        {
            return;
        }

        SelectedNode = null;
        SelectionChanged?.Invoke();
    }
}
