using ADHD.Backend.Auth;
using Microsoft.AspNetCore.RateLimiting;

namespace ADHD.Backend.Link;

public sealed record CreateLinkRequest(string? Payload);

public sealed record ClaimLinkRequest(string? Code);

/// <summary>
/// A short code that lets a child's phone receive what the parent's phone
/// shows in its QR code.
/// </summary>
public static class LinkEndpoints
{
    private const string Prefix = "mazid1.";

    public static void MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/link/create", Create)
            .RequireRateLimiting(RateLimitPolicies.Send);

        app.MapPost("/api/link/claim", Claim)
            .RequireRateLimiting(RateLimitPolicies.Verify);
    }

    private static IResult Create(
        CreateLinkRequest request,
        LinkCodeService service)
    {
        var payload = request.Payload;
        if (string.IsNullOrEmpty(payload) ||
            payload.Length > LinkCodeService.MaxPayloadLength ||
            !payload.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return Results.BadRequest(new
            {
                message = "This is not a MAZID child code."
            });
        }

        var code = service.Create(payload);
        if (code is null)
        {
            return Results.Problem(
                "Could not create a code right now. Please try again.");
        }

        return Results.Ok(new
        {
            code,
            expiresInSeconds = (int)LinkCodeService.Lifetime.TotalSeconds
        });
    }

    private static IResult Claim(
        ClaimLinkRequest request,
        LinkCodeService service)
    {
        var code = LinkCodeService.Normalize(request.Code ?? string.Empty);
        if (code.Length != LinkCodeService.CodeLength)
        {
            return Results.BadRequest(new
            {
                message = "The code has 8 letters and numbers."
            });
        }

        var payload = service.Claim(code);
        if (payload is null)
        {
            return Results.NotFound(new
            {
                message = "This code is wrong or has expired."
            });
        }

        return Results.Ok(new { payload });
    }
}
