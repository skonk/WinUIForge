namespace WinUIForge.Port.Core;

public sealed record ForgeDocumentSnapshot(string Source, string? SelectionIdentity);

public sealed record ForgeDocumentTransaction(
    string Description,
    ForgeDocumentSnapshot Before,
    ForgeDocumentSnapshot After);

public sealed class ForgeEditHistory
{
    readonly List<ForgeDocumentTransaction> undo = [];
    readonly List<ForgeDocumentTransaction> redo = [];

    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public string? UndoDescription => undo.Count == 0 ? null : undo[^1].Description;
    public string? RedoDescription => redo.Count == 0 ? null : redo[^1].Description;

    public void Clear()
    {
        undo.Clear();
        redo.Clear();
    }

    public void Record(
        string description,
        string beforeSource,
        string afterSource,
        string? beforeSelection,
        string? afterSelection)
    {
        if (string.Equals(beforeSource, afterSource, StringComparison.Ordinal))
            return;

        undo.Add(new ForgeDocumentTransaction(
            description,
            new ForgeDocumentSnapshot(beforeSource, beforeSelection),
            new ForgeDocumentSnapshot(afterSource, afterSelection)));
        redo.Clear();
    }

    public ForgeDocumentSnapshot? Undo()
    {
        if (undo.Count == 0) return null;
        var transaction = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        redo.Add(transaction);
        return transaction.Before;
    }

    public ForgeDocumentSnapshot? Redo()
    {
        if (redo.Count == 0) return null;
        var transaction = redo[^1];
        redo.RemoveAt(redo.Count - 1);
        undo.Add(transaction);
        return transaction.After;
    }
}
