using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ADHD.Backend.Auth;

/// <summary>
/// Limits how often one IP address can ask for or try verification codes.
/// Without limits anyone could send email or SMS to other people through
/// this server, or guess a code by trying every number.
/// </summary>
public static class RateLimitPolicies
{
    public const string Send = "otp-send";
    public const string Verify = "otp-verify";

    public static void AddOtpRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Every message uses the balance of the SIM card or the quota
            // of the email account.
            options.AddPolicy(Send, httpContext => ForIp(httpContext, 5));
            options.AddPolicy(Verify, httpContext => ForIp(httpContext, 30));

            // Also stops anyone from guessing the admin password.
            options.AddPolicy(
                Shadows.ShadowEndpoints.AdminPolicy,
                httpContext => ForIp(httpContext, 20));
        });
    }

    private static RateLimitPartition<string> ForIp(
        HttpContext httpContext,
        int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1)
            });
}
