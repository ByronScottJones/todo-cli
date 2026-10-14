using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Todo.Core;
using Todo.Core.Model;
using Todo.Tui.Model;

namespace Todo.Tui.Views;

/// <summary>
/// Create/edit form for one item. Errors are shown beside the fields (prefixed with "!") and [ OK ] stays
/// disabled until the input is valid. Moving an item to another list is not supported, so the list is
/// read-only when editing.
/// </summary>
public sealed class ItemDialog : OkCancelDialog
{
    private readonly TextField _subject;
    private readonly TextField _dueDate;
    private readonly CheckBox _important;
    private readonly Label _subjectError;
    private readonly Label _dueDateError;
    private readonly Button? _listButton;
    private readonly IReadOnlyList<TodoList> _lists;
    private readonly TuiTheme _theme;
    private string _listId;

    public ItemDialog(string title, IReadOnlyList<TodoList> lists, string listId, TodoItem? existing, TuiTheme theme)
        : base(title, theme)
    {
        _lists = lists;
        _theme = theme;
        _listId = listId;

        Width = Dim.Auto(DimAutoStyle.Content, minimumContentDim: Dim.Absolute(64), maximumContentDim: Dim.Percent(95));
        Height = 17;

        Add(new Label { Text = "_Subject:", X = 1, Y = 1 });
        _subject = new TextField { X = 1, Y = 2, Width = Dim.Fill(1), Value = existing?.Subject ?? string.Empty };
        Add(_subject);
        _subjectError = ErrorLabel(3);

        Add(new Label { Text = $"_Due date ({DueDateParser.FormatHint}; blank for none):", X = 1, Y = 4 });
        _dueDate = new TextField
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill(1),
            Value = existing?.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty
        };
        Add(_dueDate);
        _dueDateError = ErrorLabel(6);

        _important = new CheckBox
        {
            Text = "_Important",
            X = 1,
            Y = 8,
            Value = (existing?.IsImportant ?? false) ? CheckState.Checked : CheckState.UnChecked
        };
        Add(_important);

        Add(new Label { Text = "_List:", X = 1, Y = 10 });
        if (existing is null)
        {
            _listButton = new Button { Text = ListName(), X = 8, Y = 10, NoDecorations = false };
            _listButton.Accepting += (_, e) =>
            {
                e.Handled = true;
                ChooseList();
            };
            Add(_listButton);
        }
        else
        {
            Add(new Label { Text = ListName(), X = 8, Y = 10 });
        }

        GuardEnter(_subject);
        GuardEnter(_dueDate);
        _subject.ValueChanged += (_, _) => Revalidate();
        _dueDate.ValueChanged += (_, _) => Revalidate();
        Revalidate();
        FocusOnOpen(_subject);
    }

    /// <summary>The entered values; only meaningful after the dialog was confirmed.</summary>
    public ItemDraft Draft => new(
        _subject.Value ?? string.Empty,
        DueDateParser.Parse(_dueDate.Value),
        _important.Value == CheckState.Checked,
        _listId);

    /// <summary>Current validation state, exposed so tests and callers can inspect it.</summary>
    public ValidationResult Validation => ItemValidator.Validate(_subject.Value, _dueDate.Value);

    protected override bool CanConfirm() => Validation.IsValid;

    private Label ErrorLabel(int y)
    {
        var label = new Label { Text = string.Empty, X = 1, Y = y, Width = Dim.Fill(1) };
        label.SetScheme(_theme.DialogError);
        Add(label);
        return label;
    }

    private string ListName() => _lists.FirstOrDefault(l => l.Id == _listId)?.Name ?? "(none)";

    private void Revalidate()
    {
        var result = Validation;
        _subjectError.Text = result.SubjectError is null ? string.Empty : "! " + result.SubjectError;
        _dueDateError.Text = result.DueDateError is null ? string.Empty : "! " + result.DueDateError;
        OkButton.Enabled = result.IsValid;
    }

    private void ChooseList()
    {
        if (App is null || _listButton is null || _lists.Count == 0)
            return;

        var names = _lists.Select(l => l.Name ?? "(unnamed)").ToList();
        var current = _lists.ToList().FindIndex(l => l.Id == _listId);
        using var picker = new ListPickerDialog("Choose list", names, Math.Max(0, current), _theme);
        App.Run(picker);
        if (picker.Confirmed && picker.SelectedIndex >= 0 && picker.SelectedIndex < _lists.Count)
        {
            _listId = _lists[picker.SelectedIndex].Id ?? _listId;
            _listButton.Text = ListName();
        }

        _listButton.SetFocus();
    }
}
