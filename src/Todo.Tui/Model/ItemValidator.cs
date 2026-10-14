using Todo.Core;

namespace Todo.Tui.Model;

/// <summary>Per-field validation errors; a null error means the field is valid.</summary>
public sealed record ValidationResult(string? SubjectError, string? DueDateError)
{
    public bool IsValid => SubjectError is null && DueDateError is null;
}

/// <summary>Validation rules for the item form. They match the CLI's <c>todo add</c> rules.</summary>
public static class ItemValidator
{
    public const string SubjectRequired = "Subject is required.";
    public static readonly string DueDateInvalid = $"Invalid due date. Use {DueDateParser.FormatHint}.";

    public static ValidationResult Validate(string? subject, string? dueDateText)
    {
        var subjectError = string.IsNullOrWhiteSpace(subject) ? SubjectRequired : null;
        var dueDateError = !string.IsNullOrWhiteSpace(dueDateText) && DueDateParser.Parse(dueDateText) is null
            ? DueDateInvalid
            : null;
        return new ValidationResult(subjectError, dueDateError);
    }
}
