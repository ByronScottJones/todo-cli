using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Todo.Core.Model;
using Todo.Core.Repository;
using Todo.Tui.Model;
using Xunit;

namespace Todo.CLI.Tests.Tui;

public class TodoWorkspaceTests
{
    private readonly Mock<ITodoItemRepository> _items = new();
    private readonly Mock<ITodoListRepository> _lists = new();
    private readonly TodoWorkspace _workspace;
    private List<TodoItem> _stored = [];

    public TodoWorkspaceTests()
    {
        _lists.Setup(l => l.GetAllAsync()).ReturnsAsync(() => new List<TodoList>
        {
            new() { Id = "L1", Name = "Inbox" },
            new() { Id = "L2", Name = "Work" }
        });
        _items.Setup(i => i.ListAllAsync(true)).ReturnsAsync(() => _stored.ToList());
        _workspace = new TodoWorkspace(_items.Object, _lists.Object);
    }

    private static TodoItem Item(string id, string subject, string listId = "L1", bool done = false,
        bool important = false, DateTime? due = null) => new()
    {
        Id = id, Subject = subject, ListId = listId, IsCompleted = done, IsImportant = important, DueDate = due,
        Status = done ? "Completed" : "NotStarted"
    };

    private async Task LoadAsync(params TodoItem[] items)
    {
        _stored = items.ToList();
        await _workspace.LoadAsync();
    }

    [Fact]
    public async Task LoadAsync_RaisesChangedAndExposesListsAndItems()
    {
        var raised = 0;
        _workspace.Changed += () => raised++;

        await LoadAsync(Item("1", "a"), Item("2", "b", done: true));

        Assert.Equal(1, raised);
        Assert.Equal(2, _workspace.Lists.Count);
        Assert.Equal(2, _workspace.TotalCount);
        Assert.Equal(1, _workspace.OpenCount);
    }

    [Fact]
    public async Task VisibleItems_HidesCompletedUntilRequested()
    {
        await LoadAsync(Item("1", "a"), Item("2", "b", done: true));

        Assert.Single(_workspace.VisibleItems);

        _workspace.SetShowCompleted(true);

        Assert.Equal(2, _workspace.VisibleItems.Count);
    }

    [Fact]
    public async Task VisibleItems_OrdersOpenFirstThenImportantThenDueDateThenSubject()
    {
        await LoadAsync(
            Item("done", "done", done: true),
            Item("plain-b", "b"),
            Item("plain-a", "a"),
            Item("due-late", "late", due: new DateTime(2026, 12, 1)),
            Item("due-soon", "soon", due: new DateTime(2026, 11, 1)),
            Item("star", "star", important: true));
        _workspace.SetShowCompleted(true);

        var ids = _workspace.VisibleItems.Select(i => i.Id).ToArray();

        Assert.Equal(new[] { "star", "due-soon", "due-late", "plain-a", "plain-b", "done" }, ids);
    }

    [Fact]
    public async Task SelectList_FiltersToThatList()
    {
        await LoadAsync(Item("1", "a", "L1"), Item("2", "b", "L2"));

        _workspace.SelectList("L2");

        Assert.Equal("2", Assert.Single(_workspace.VisibleItems).Id);
        Assert.Equal("Work", _workspace.SelectedListName);

        _workspace.SelectList(null);

        Assert.Equal(2, _workspace.VisibleItems.Count);
        Assert.Equal("All lists", _workspace.SelectedListName);
    }

    [Fact]
    public async Task SetFilter_MatchesSubjectCaseInsensitivelyAndTrims()
    {
        await LoadAsync(Item("1", "Buy Milk"), Item("2", "Call mum"));

        _workspace.SetFilter("  milk ");

        Assert.Equal("1", Assert.Single(_workspace.VisibleItems).Id);
        Assert.Equal("milk", _workspace.FilterText);

        _workspace.SetFilter(null);

        Assert.Equal(2, _workspace.VisibleItems.Count);
    }

    [Fact]
    public async Task SetFilter_NoMatches_ReturnsEmpty()
    {
        await LoadAsync(Item("1", "a"));

        _workspace.SetFilter("zzz");

        Assert.Empty(_workspace.VisibleItems);
    }

    [Fact]
    public async Task LoadAsync_SelectedListNoLongerExists_FallsBackToAllLists()
    {
        await LoadAsync(Item("1", "a"));
        _workspace.SelectList("L2");
        _lists.Setup(l => l.GetAllAsync()).ReturnsAsync(new List<TodoList> { new() { Id = "L1", Name = "Inbox" } });

        await _workspace.LoadAsync();

        Assert.Null(_workspace.SelectedListId);
    }

    [Fact]
    public async Task LoadAsync_EmptyAccount_YieldsEmptyState()
    {
        await LoadAsync();

        Assert.Empty(_workspace.VisibleItems);
        Assert.Equal(0, _workspace.TotalCount);
    }

    [Fact]
    public async Task LoadAsync_ServiceFails_PropagatesAndKeepsPreviousData()
    {
        await LoadAsync(Item("1", "a"));
        _items.Setup(i => i.ListAllAsync(true)).ThrowsAsync(new InvalidOperationException("offline"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _workspace.LoadAsync());

        Assert.Single(_workspace.VisibleItems);
    }

    [Fact]
    public async Task CreateAsync_AddsItemWithDraftValuesThenReloads()
    {
        await LoadAsync();
        TodoItem? added = null;
        _items.Setup(i => i.AddAsync(It.IsAny<TodoItem>())).Callback<TodoItem>(i => added = i).Returns(Task.CompletedTask);

        await _workspace.CreateAsync(new ItemDraft("Pay rent", new DateTime(2026, 11, 3), true, "L2"));

        Assert.NotNull(added);
        Assert.Equal("Pay rent", added!.Subject);
        Assert.Equal("L2", added.ListId);
        Assert.True(added.IsImportant);
        Assert.Equal(new DateTime(2026, 11, 3), added.DueDate);
        _items.Verify(i => i.ListAllAsync(true), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAsync_ServiceFails_PropagatesWithoutReload()
    {
        await LoadAsync();
        _items.Setup(i => i.AddAsync(It.IsAny<TodoItem>())).ThrowsAsync(new InvalidOperationException("denied"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _workspace.CreateAsync(new ItemDraft("x", null, false, "L1")));

        _items.Verify(i => i.ListAllAsync(true), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_SendsEditedCopyAndLeavesOriginalUntouched()
    {
        var original = Item("1", "old", due: new DateTime(2026, 11, 1));
        await LoadAsync(original);
        TodoItem? sent = null;
        _items.Setup(i => i.UpdateAsync(It.IsAny<TodoItem>())).Callback<TodoItem>(i => sent = i).Returns(Task.CompletedTask);

        await _workspace.UpdateAsync(original, new ItemDraft("new", null, true, "L1"));

        Assert.Equal("new", sent!.Subject);
        Assert.Null(sent.DueDate);
        Assert.True(sent.IsImportant);
        Assert.Equal("1", sent.Id);
        Assert.Equal("L1", sent.ListId);
        Assert.Equal("old", original.Subject);
    }

    [Fact]
    public async Task ToggleCompleteAsync_OpenItem_SendsCompletedItem()
    {
        var item = Item("1", "a");
        await LoadAsync(item);
        TodoItem? sent = null;
        _items.Setup(i => i.UpdateAsync(It.IsAny<TodoItem>())).Callback<TodoItem>(i => sent = i).Returns(Task.CompletedTask);

        await _workspace.ToggleCompleteAsync(item);

        Assert.True(sent!.IsCompleted);
        Assert.Equal("Completed", sent.Status);
        Assert.False(item.IsCompleted);
    }

    [Fact]
    public async Task ToggleCompleteAsync_CompletedItem_ReopensIt()
    {
        var item = Item("1", "a", done: true);
        await LoadAsync(item);
        TodoItem? sent = null;
        _items.Setup(i => i.UpdateAsync(It.IsAny<TodoItem>())).Callback<TodoItem>(i => sent = i).Returns(Task.CompletedTask);

        await _workspace.ToggleCompleteAsync(item);

        Assert.False(sent!.IsCompleted);
        Assert.Equal("NotStarted", sent.Status);
    }

    [Fact]
    public async Task ToggleImportantAsync_FlipsImportanceOnly()
    {
        var item = Item("1", "a", due: new DateTime(2026, 11, 1));
        await LoadAsync(item);
        TodoItem? sent = null;
        _items.Setup(i => i.UpdateAsync(It.IsAny<TodoItem>())).Callback<TodoItem>(i => sent = i).Returns(Task.CompletedTask);

        await _workspace.ToggleImportantAsync(item);

        Assert.True(sent!.IsImportant);
        Assert.Equal(item.DueDate, sent.DueDate);
        Assert.Equal("a", sent.Subject);
    }

    [Fact]
    public async Task DeleteAsync_DeletesThenReloads()
    {
        var item = Item("1", "a");
        await LoadAsync(item);
        _items.Setup(i => i.DeleteAsync(item)).Callback(() => _stored.Clear()).Returns(Task.CompletedTask);

        await _workspace.DeleteAsync(item);

        _items.Verify(i => i.DeleteAsync(item), Times.Once);
        Assert.Empty(_workspace.VisibleItems);
    }

    [Fact]
    public async Task DefaultListId_PrefersSelectedListThenServiceDefaultThenFirstList()
    {
        _lists.Setup(l => l.GetDefaultListAsync()).ReturnsAsync(new TodoList { Id = "L2" });
        await LoadAsync();

        Assert.Equal("L2", _workspace.DefaultListId);

        _workspace.SelectList("L1");
        Assert.Equal("L1", _workspace.DefaultListId);

        _workspace.SelectList(null);
        _lists.Setup(l => l.GetDefaultListAsync()).ReturnsAsync((TodoList?)null);
        await _workspace.LoadAsync();
        Assert.Equal("L1", _workspace.DefaultListId);
    }

    [Fact]
    public async Task DefaultListId_NoLists_IsNull()
    {
        _lists.Setup(l => l.GetAllAsync()).ReturnsAsync(new List<TodoList>());
        await LoadAsync();

        Assert.Null(_workspace.DefaultListId);
    }

    [Fact]
    public async Task ListName_UnknownId_ReturnsPlaceholder()
    {
        await LoadAsync();

        Assert.Equal("(unknown list)", _workspace.ListName("nope"));
        Assert.Equal("Inbox", _workspace.ListName("L1"));
    }
}
