using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Todo.Core.Model;
using Todo.Core.Repository;

namespace Todo.CLI.Tests.Tui;

/// <summary>In-memory item and list repositories with call recording and failure injection.</summary>
internal sealed class FakeTodoService : ITodoItemRepository, ITodoListRepository
{
    private int _nextId = 100;

    public List<TodoItem> Items { get; } = [];

    public List<TodoList> Lists { get; } =
    [
        new() { Id = "L1", Name = "Inbox" },
        new() { Id = "L2", Name = "Work" }
    ];

    public List<TodoItem> Updated { get; } = [];

    public List<TodoItem> Added { get; } = [];

    public List<TodoItem> Deleted { get; } = [];

    public int LoadCount { get; private set; }

    /// <summary>When set, the next service call throws this and clears it.</summary>
    public Exception? FailNext { get; set; }

    public TodoItem Add(string subject, string listId = "L1", bool done = false, bool important = false, DateTime? due = null)
    {
        var item = new TodoItem
        {
            Id = (_nextId++).ToString(), Subject = subject, ListId = listId, IsCompleted = done,
            IsImportant = important, DueDate = due, Status = done ? "Completed" : "NotStarted"
        };
        Items.Add(item);
        return item;
    }

    private void MaybeFail()
    {
        if (FailNext is null)
            return;
        var ex = FailNext;
        FailNext = null;
        throw ex;
    }

    public Task AddAsync(TodoItem item)
    {
        MaybeFail();
        Added.Add(item);
        Add(item.Subject ?? string.Empty, item.ListId ?? "L1", item.IsCompleted, item.IsImportant, item.DueDate);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<TodoItem>> ListAllAsync(bool includeCompleted)
    {
        MaybeFail();
        LoadCount++;
        return Task.FromResult<IEnumerable<TodoItem>>(Items.Select(Clone).ToList());
    }

    public async IAsyncEnumerable<TodoItem> EnumerateAllAsync(bool includeCompleted)
    {
        foreach (var item in Items)
            yield return Clone(item);
        await Task.CompletedTask;
    }

    public Task<IEnumerable<TodoItem>> ListByListIdAsync(string listId, bool includeCompleted) => ListAllAsync(includeCompleted);

    public Task<IEnumerable<TodoItem>> ListByListNameAsync(string listName, bool includeCompleted) => ListAllAsync(includeCompleted);

    public Task CompleteAsync(TodoItem item) => UpdateAsync(item);

    public Task UpdateAsync(TodoItem item)
    {
        MaybeFail();
        Updated.Add(Clone(item));
        var index = Items.FindIndex(i => i.Id == item.Id);
        if (index >= 0)
            Items[index] = Clone(item);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TodoItem item)
    {
        MaybeFail();
        Deleted.Add(item);
        Items.RemoveAll(i => i.Id == item.Id);
        return Task.CompletedTask;
    }

    public Task AddAsync(TodoList list) => Task.CompletedTask;

    public Task<IEnumerable<TodoList>> GetAllAsync() => Task.FromResult<IEnumerable<TodoList>>(Lists.ToList());

    public Task<TodoList?> GetByNameAsync(string name) => Task.FromResult(Lists.FirstOrDefault(l => l.Name == name));

    public Task<TodoList?> GetDefaultListAsync() => Task.FromResult(Lists.FirstOrDefault());

    public Task DeleteAsync(TodoList list) => Task.CompletedTask;

    private static TodoItem Clone(TodoItem i) => new()
    {
        Id = i.Id, Subject = i.Subject, ListId = i.ListId, IsCompleted = i.IsCompleted, IsImportant = i.IsImportant,
        DueDate = i.DueDate, Status = i.Status, Created = i.Created, Completed = i.Completed
    };
}
