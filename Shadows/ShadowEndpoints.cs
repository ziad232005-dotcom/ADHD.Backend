using System.Security.Cryptography;
using System.Text;
using ADHD.Backend.Auth;
using Microsoft.AspNetCore.RateLimiting;

namespace ADHD.Backend.Shadows;

public sealed record RejectRequest(string? Reason);

/// <summary>
/// Shadow Teachers apply, the admin approves, parents read the approved list.
/// </summary>
public static class ShadowEndpoints
{
    public const string AdminPolicy = "shadow-admin";

    public static void MapShadowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shadows", Apply)
            .RequireRateLimiting(RateLimitPolicies.Send);

        app.MapGet("/api/shadows", Approved)
            .RequireRateLimiting(RateLimitPolicies.Verify);

        var admin = app.MapGroup("/api/admin/shadows")
            .RequireRateLimiting(AdminPolicy);

        admin.MapGet("", AdminList);
        admin.MapGet("{id:int}/documents/{index:int}", AdminDocument);
        admin.MapGet("{id:int}/photo", AdminPhoto);
        admin.MapPost("{id:int}/approve", AdminApprove);
        admin.MapPost("{id:int}/reject", AdminReject);
    }

    private static IResult Apply(
        ShadowApplicationRequest request,
        ShadowStore store)
    {
        var error = ShadowValidation.Validate(request, out var application);
        if (error is not null || application is null)
        {
            return Results.BadRequest(new
            {
                message = error ?? "The application is not valid."
            });
        }

        var saved = store.Add(application);
        if (saved is null)
        {
            return Results.Problem(
                "Applications are closed right now. Please try later.");
        }

        return Results.Ok(new { id = saved.Id });
    }

    private static IResult Approved(ShadowStore store)
    {
        var shadows = store.WithStatus(ShadowStatus.Approved).Select(s => new
        {
            s.Id,
            s.Name,
            s.Governorate,
            s.YearsExperience,
            s.Qualification,
            s.University,
            s.CurrentWorkplace,
            s.ChildExperience,
            s.AdhdExperience,
            s.Skills,
            Phone = s.Mobile,
            s.Email
        });

        return Results.Ok(new { shadows });
    }

    // ---- Admin ----

    /// <summary>
    /// Null when the password is right, otherwise the answer to send back.
    /// </summary>
    private static IResult? CheckAdmin(HttpContext context, IConfiguration configuration)
    {
        var expected = configuration["Admin:Password"];
        if (string.IsNullOrEmpty(expected))
        {
            return Results.Problem(
                "The admin password is not set on the server.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var given = context.Request.Headers["X-Admin-Password"].ToString();

        // Compare hashes so the length of the password does not leak.
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(given));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(a, b)
            ? null
            : Results.Json(
                new { message = "Wrong password." },
                statusCode: StatusCodes.Status401Unauthorized);
    }

    private static IResult AdminList(
        HttpContext context,
        IConfiguration configuration,
        ShadowStore store,
        string? status)
    {
        if (CheckAdmin(context, configuration) is { } denied) return denied;

        var wanted = Enum.TryParse<ShadowStatus>(status, true, out var parsed)
            ? parsed
            : ShadowStatus.Pending;

        return Results.Ok(new { shadows = store.WithStatus(wanted) });
    }

    private static IResult AdminDocument(
        int id,
        int index,
        HttpContext context,
        IConfiguration configuration,
        ShadowStore store)
    {
        if (CheckAdmin(context, configuration) is { } denied) return denied;

        var document = store.Document(id, index);
        return document is null
            ? Results.NotFound(new { message = "No such document." })
            : Results.File(document.Value.Path, document.Value.ContentType);
    }

    private static IResult AdminPhoto(
        int id,
        HttpContext context,
        IConfiguration configuration,
        ShadowStore store)
    {
        if (CheckAdmin(context, configuration) is { } denied) return denied;

        var photo = store.Photo(id);
        return photo is null
            ? Results.NotFound(new { message = "No photo." })
            : Results.File(photo.Value.Path, photo.Value.ContentType);
    }

    private static IResult AdminApprove(
        int id,
        HttpContext context,
        IConfiguration configuration,
        ShadowStore store)
    {
        if (CheckAdmin(context, configuration) is { } denied) return denied;

        return store.Review(id, ShadowStatus.Approved, null)
            ? Results.Ok(new { message = "Approved." })
            : Results.NotFound(new { message = "No such application." });
    }

    private static IResult AdminReject(
        int id,
        RejectRequest request,
        HttpContext context,
        IConfiguration configuration,
        ShadowStore store)
    {
        if (CheckAdmin(context, configuration) is { } denied) return denied;

        var reason = request.Reason?.Trim();
        if (reason is { Length: > 300 }) reason = reason[..300];

        return store.Review(id, ShadowStatus.Rejected, reason)
            ? Results.Ok(new { message = "Rejected." })
            : Results.NotFound(new { message = "No such application." });
    }
}
