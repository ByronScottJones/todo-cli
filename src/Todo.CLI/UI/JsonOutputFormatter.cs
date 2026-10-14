using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Todo.Core.Model;

namespace Todo.CLI.UI;

public class JsonOutputFormatter : IOutputFormatter
{
    public string FormatSuccess(string message)
    {
        var json = JsonSerializer.Serialize(new StatusOutput(true, message), JsonOutputContext.Default.StatusOutput);
        Console.WriteLine(json);
        return json;
    }

    public string FormatError(string message)
    {
        var json = JsonSerializer.Serialize(new StatusOutput(false, message), JsonOutputContext.Default.StatusOutput);
        Console.Error.WriteLine(json);
        return json;
    }

    public string FormatList(TodoList list, bool noStatus)
    {
        var output = new ListOutput(list.Name, list.Count, list.Tasks.Select(t => FormatItemToJson(t, noStatus)).ToList());
        var json = JsonSerializer.Serialize(output, JsonOutputContext.Default.ListOutput);
        Console.WriteLine(json);
        return json;
    }

    public string FormatItem(TodoItem item, bool noStatus)
    {
        var json = JsonSerializer.Serialize(FormatItemToJson(item, noStatus), JsonOutputContext.Default.ItemOutput);
        Console.WriteLine(json);
        return json;
    }

    public string FormatItems(IEnumerable<TodoItem> items, bool noStatus)
    {
        var output = items.Select(item => FormatItemToJson(item, noStatus)).ToList();
        var json = JsonSerializer.Serialize<IReadOnlyList<ItemOutput>>(output, JsonOutputContext.Default.IReadOnlyListItemOutput);
        Console.WriteLine(json);
        return json;
    }

    private static ItemOutput FormatItemToJson(TodoItem item, bool noStatus)
    {
        return new ItemOutput(
            item.Id,
            item.Subject,
            item.IsCompleted,
            item.Status,
            item.Completed?.ToString("yyyy-MM-dd"),
            item.DueDate?.ToString("yyyy-MM-dd"),
            item.ListId,
            noStatus ? item.Subject : item.ToString());
    }
}
