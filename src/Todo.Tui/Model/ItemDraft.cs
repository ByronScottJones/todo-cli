namespace Todo.Tui.Model;

/// <summary>User-entered values for creating or editing an item.</summary>
public sealed record ItemDraft(string Subject, DateTime? DueDate, bool IsImportant, string ListId);
