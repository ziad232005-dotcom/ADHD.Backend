using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.RateLimiting;

namespace ADHD.Backend.Sync;

public sealed record PushRequest(
    string? Key, string? Kind, string? Id, JsonElement Data);

public sealed record PullRequest(string? Key);

public sealed record PutTasksRequest(string? Key, JsonElement Data);

/// <summary>
/// Moves test results from the child's phone to the parent's phone. The
/// key is a secret made on the parent's phone, so it travels in the body
/// and never in a web address.
/// </summary>
public static partial class SyncEndpoints
{
    public const string Policy = "sync";

    [GeneratedRegex("^[A-Za-z0-9_-]{20,64}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[0-9]{1,20}$")]
    private static partial Regex IdPattern();

    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sync/push", Push).RequireRateLimiting(Policy);
        app.MapPost("/api/sync/pull", Pull).RequireRateLimiting(Policy);
        app.MapPost("/api/sync/tasks/put", PutTasks).RequireRateLimiting(Policy);
        app.MapPost("/api/sync/tasks/get", GetTasks).RequireRateLimiting(Policy);
    }

    private static IResult Push(PushRequest request, SyncStore store)
    {
        if (request.Key is null || !KeyPattern().IsMatch(request.Key) ||
            request.Kind is not ("tova" or "cars") ||
            request.Id is null || !IdPattern().IsMatch(request.Id) ||
            request.Data.ValueKind != JsonValueKind.Object ||
            request.Data.GetRawText().Length > SyncStore.MaxDataLength)
        {
            return Results.BadRequest(new { message = "This result is not valid." });
        }

        return store.Add(request.Key, request.Kind, request.Id, request.Data)
            ? Results.Ok(new { saved = true })
            : Results.Problem("The server cannot keep more results.");
    }

    private static IResult Pull(PullRequest request, SyncStore store)
    {
        if (request.Key is null || !KeyPattern().IsMatch(request.Key))
        {
            return Results.BadRequest(new { message = "This key is not valid." });
        }

        var results = store.Results(request.Key)
            .Select(r => new { id = r.Id, kind = r.Kind, data = r.Data });

        return Results.Ok(new { results });
    }

    private static IResult PutTasks(PutTasksRequest request, SyncStore store)
    {
        if (request.Key is null || !KeyPattern().IsMatch(request.Key) ||
            request.Data.ValueKind != JsonValueKind.Object ||
            request.Data.GetRawText().Length > SyncStore.MaxTasksLength)
        {
            return Results.BadRequest(new { message = "These tasks are not valid." });
        }

        return store.PutTasks(request.Key, request.Data)
            ? Results.Ok(new { saved = true })
            : Results.Problem("The server cannot keep more tasks.");
    }

    private static IResult GetTasks(PullRequest request, SyncStore store)
    {
        if (request.Key is null || !KeyPattern().IsMatch(request.Key))
        {
            return Results.BadRequest(new { message = "This key is not valid." });
        }

        var snapshot = store.GetTasks(request.Key);
        return snapshot is null
            ? Results.Ok(new { data = (object?)null })
            : Results.Ok(new { data = snapshot.Data, updatedAt = snapshot.UpdatedAt });
    }
}
