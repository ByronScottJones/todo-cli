using System;
using System.IO;
using System.Text;
using Microsoft.Kiota.Serialization.Json;
using Todo.Core.Model;
using Todo.Core.Repository;
using Xunit;

namespace Todo.CLI.Tests.Repository;

public class TodoItemUpdateTests
{
    private static string Serialize(TodoItem item)
    {
        using var writer = new JsonSerializationWriter();
        writer.WriteObjectValue(null, TodoItemRepository.BuildUpdate(item));
        using var stream = writer.GetSerializedContent();
        return new StreamReader(stream, Encoding.UTF8).ReadToEnd();
    }

    [Fact]
    public void BuildUpdate_NoDueDate_SendsExplicitNullToClearIt()
    {
        var json = Serialize(new TodoItem { Id = "1", ListId = "L", Subject = "x", DueDate = null });

        Assert.Contains("\"dueDateTime\":null", json);
    }

    [Fact]
    public void BuildUpdate_WithDueDate_SendsDateAndTimeZone()
    {
        var json = Serialize(new TodoItem { Id = "1", ListId = "L", Subject = "x", DueDate = new DateTime(2026, 11, 5) });

        Assert.Contains("2026-11-05T00:00:00", json);
    }

    [Fact]
    public void BuildUpdate_CompletedImportantItem_MapsStatusAndImportance()
    {
        var json = Serialize(new TodoItem { Id = "1", ListId = "L", Subject = "x", IsCompleted = true, IsImportant = true });

        Assert.Contains("\"status\":\"completed\"", json);
        Assert.Contains("\"importance\":\"high\"", json);
    }

    [Fact]
    public void BuildUpdate_OpenNormalItem_MapsStatusAndImportance()
    {
        var json = Serialize(new TodoItem { Id = "1", ListId = "L", Subject = "x" });

        Assert.Contains("\"status\":\"notStarted\"", json);
        Assert.Contains("\"importance\":\"normal\"", json);
    }
}
