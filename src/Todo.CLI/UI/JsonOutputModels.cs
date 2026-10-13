using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Todo.CLI.UI;

// Wire-format models for JSON output. Property names are part of the CLI's output contract.
internal sealed record StatusOutput(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message);

internal sealed record ItemOutput(
    string? Id,
    string? Subject,
    bool IsCompleted,
    string Status,
    string? Completed,
    string? DueDate,
    string? ListId,
    string? DisplayText);

internal sealed record ListOutput(string? Name, int Count, IReadOnlyList<ItemOutput> Tasks);

// Source-generated serialization keeps JSON output trim- and AOT-safe.
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StatusOutput))]
[JsonSerializable(typeof(ItemOutput))]
[JsonSerializable(typeof(ListOutput))]
[JsonSerializable(typeof(IReadOnlyList<ItemOutput>))]
internal sealed partial class JsonOutputContext : JsonSerializerContext
{
}
