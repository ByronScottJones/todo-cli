using Todo.Core.Model;
using Todo.Core.Repository;

namespace Todo.Tui.Model;

/// <summary>
/// UI-independent state and operations behind the TUI: loaded lists and items, the active list/filter,
/// and create/update/complete/delete. Every mutation reloads from the service so the view never drifts.
/// </summary>
public sealed class TodoWorkspace
{
    private readonly ITodoItemRepository _items;
    private readonly ITodoListRepository _lists;
    private List<TodoList> _allLists = [];
    private List<TodoItem> _allItems = [];
    private string? _serviceDefaultListId;

    public TodoWorkspace(ITodoItemRepository items, ITodoListRepository lists)
    {
        _items = items;
        _lists = lists;
    }

    public event Action? Changed;

    public IReadOnlyList<TodoList> Lists => _allLists;

    /// <summary>The list being shown, or null for all lists.</summary>
    public string? SelectedListId { get; private set; }

    public bool ShowCompleted { get; private set; }

    public string FilterText { get; private set; } = string.Empty;

    public string SelectedListName => SelectedListId is null ? "All lists" : ListName(SelectedListId);

    public int TotalCount => _allItems.Count;

    public int OpenCount => _allItems.Count(i => !i.IsCompleted);

    /// <summary>Items matching the list, completed and text filters, in display order.</summary>
    public IReadOnlyList<TodoItem> VisibleItems => BuildVisible();

    public string ListName(string? listId) =>
        _allLists.FirstOrDefault(l => l.Id == listId)?.Name ?? "(unknown list)";

    public async Task LoadAsync()
    {
        var lists = (await _lists.GetAllAsync()).ToList();
        var items = (await _items.ListAllAsync(includeCompleted: true)).ToList();
        var defaultList = await _lists.GetDefaultListAsync();
        _allLists = lists;
        _allItems = items;
        _serviceDefaultListId = defaultList?.Id;
        if (SelectedListId is not null && lists.All(l => l.Id != SelectedListId))
            SelectedListId = null;
        Changed?.Invoke();
    }

    public void SelectList(string? listId)
    {
        SelectedListId = listId;
        Changed?.Invoke();
    }

    public void SetShowCompleted(bool show)
    {
        ShowCompleted = show;
        Changed?.Invoke();
    }

    public void SetFilter(string? text)
    {
        FilterText = text?.Trim() ?? string.Empty;
        Changed?.Invoke();
    }

    /// <summary>List id new items default to: the selected list, else the service default, else the first list.</summary>
    public string? DefaultListId => SelectedListId ?? _serviceDefaultListId ?? _allLists.FirstOrDefault()?.Id;

    public async Task CreateAsync(ItemDraft draft)
    {
        await _items.AddAsync(new TodoItem
        {
            Subject = draft.Subject,
            DueDate = draft.DueDate,
            IsImportant = draft.IsImportant,
            ListId = draft.ListId
        });
        await LoadAsync();
    }

    /// <summary>Applies the draft to the item. Moving an item to another list is not supported by the service.</summary>
    public async Task UpdateAsync(TodoItem item, ItemDraft draft)
    {
        await _items.UpdateAsync(Copy(item, i =>
        {
            i.Subject = draft.Subject;
            i.DueDate = draft.DueDate;
            i.IsImportant = draft.IsImportant;
        }));
        await LoadAsync();
    }

    public async Task ToggleCompleteAsync(TodoItem item)
    {
        await _items.UpdateAsync(Copy(item, i =>
        {
            i.IsCompleted = !item.IsCompleted;
            i.Status = i.IsCompleted ? "Completed" : "NotStarted";
        }));
        await LoadAsync();
    }

    public async Task ToggleImportantAsync(TodoItem item)
    {
        await _items.UpdateAsync(Copy(item, i => i.IsImportant = !item.IsImportant));
        await LoadAsync();
    }

    public async Task DeleteAsync(TodoItem item)
    {
        await _items.DeleteAsync(item);
        await LoadAsync();
    }

    private static TodoItem Copy(TodoItem source, Action<TodoItem> change)
    {
        var copy = new TodoItem
        {
            Id = source.Id,
            Subject = source.Subject,
            IsCompleted = source.IsCompleted,
            IsImportant = source.IsImportant,
            Status = source.Status,
            Completed = source.Completed,
            Created = source.Created,
            ListId = source.ListId,
            DueDate = source.DueDate
        };
        change(copy);
        return copy;
    }

    private List<TodoItem> BuildVisible()
    {
        IEnumerable<TodoItem> query = _allItems;
        if (SelectedListId is not null)
            query = query.Where(i => i.ListId == SelectedListId);
        if (!ShowCompleted)
            query = query.Where(i => !i.IsCompleted);
        if (FilterText.Length > 0)
            query = query.Where(i => i.Subject?.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase) == true);

        // Open before completed, important first, then soonest due date (undated last), then subject.
        return query
            .OrderBy(i => i.IsCompleted)
            .ThenByDescending(i => i.IsImportant)
            .ThenBy(i => i.DueDate ?? DateTime.MaxValue)
            .ThenBy(i => i.Subject, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
