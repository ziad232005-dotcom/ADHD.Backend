using System.Net.Mail;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.RateLimiting;
using MimeKit;

namespace ADHD.Backend.Auth;

public sealed record SendOtpRequest(string? Email);

public sealed record VerifyOtpRequest(string? Email, string? Code);

/// <summary>Email verification: send a code, then check it.</summary>
public static class EmailAuthEndpoints
{
    public static void MapEmailAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/send-otp", SendOtpAsync)
            .RequireRateLimiting(RateLimitPolicies.Send);

        app.MapPost("/api/auth/verify-otp", VerifyOtp)
            .RequireRateLimiting(RateLimitPolicies.Verify);
    }

    private static async Task<IResult> SendOtpAsync(
        SendOtpRequest request,
        OtpService otpService,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeEmail(request.Email, out var email))
        {
            return Results.BadRequest(new
            {
                message = "Please enter a valid email address."
            });
        }

        if (!otpService.CanSend(email, out var retryAfter))
        {
            return Results.Json(
                new
                {
                    message = "Please wait " +
                              $"{Math.Ceiling(retryAfter.TotalSeconds)} " +
                              "seconds before requesting a new code."
                },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var code = otpService.Issue(email);

        if (!await TrySendEmailAsync(
                configuration, email, code, cancellationToken))
        {
            otpService.Discard(email);

            return Results.Problem(
                detail: "Could not send the verification email. " +
                        "Please try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new
        {
            message = "Verification code sent successfully."
        });
    }

    private static IResult VerifyOtp(
        VerifyOtpRequest request,
        OtpService otpService)
    {
        var code = request.Code ?? string.Empty;

        if (!TryNormalizeEmail(request.Email, out var email) ||
            code.Length != 6 ||
            !code.All(char.IsAsciiDigit))
        {
            return Results.BadRequest(new
            {
                message = "Invalid email or verification code."
            });
        }

        return otpService.Verify(email, code) switch
        {
            OtpVerifyResult.Verified => Results.Ok(new
            {
                message = "Email verified successfully.",
                verified = true
            }),
            OtpVerifyResult.NoActiveCode => Results.BadRequest(new
            {
                message = "No active verification code. Request a new code."
            }),
            OtpVerifyResult.Expired => Results.BadRequest(new
            {
                message = "Verification code expired. Request a new code."
            }),
            OtpVerifyResult.TooManyAttempts => Results.Json(
                new
                {
                    message = "Too many incorrect attempts. " +
                              "Request a new code."
                },
                statusCode: StatusCodes.Status429TooManyRequests),
            _ => Results.BadRequest(new
            {
                message = "Incorrect verification code."
            })
        };
    }

    /// <summary>Accepts only a plain address, not "Name &lt;a@b.com&gt;".</summary>
    private static bool TryNormalizeEmail(string? raw, out string email)
    {
        email = string.Empty;
        var text = raw?.Trim();

        if (string.IsNullOrEmpty(text) ||
            text.Length > 254 ||
            !MailAddress.TryCreate(text, out var address) ||
            !string.Equals(
                address.Address, text, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        email = address.Address.ToLowerInvariant();
        return true;
    }

    private static async Task<bool> TrySendEmailAsync(
        IConfiguration configuration,
        string email,
        string code,
        CancellationToken cancellationToken)
    {
        var senderEmail = configuration["EmailSettings:SenderEmail"];
        var appPassword = configuration["EmailSettings:AppPassword"];
        var smtpServer = configuration["EmailSettings:SmtpServer"];

        // The secrets live in User Secrets, never in the repository.
        if (string.IsNullOrWhiteSpace(senderEmail) ||
            string.IsNullOrWhiteSpace(appPassword) ||
            string.IsNullOrWhiteSpace(smtpServer))
        {
            return false;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("MAZID", senderEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Your MAZID Verification Code";
        message.Body = new TextPart("plain")
        {
            Text =
                $"Your MAZID verification code is: {code}\n\n" +
                "This code expires in 5 minutes.\n\n" +
                "If you did not request this code, please ignore this email."
        };

        try
        {
            using var smtp = new MailKit.Net.Smtp.SmtpClient();

            await smtp.ConnectAsync(
                smtpServer, 587, SecureSocketOptions.StartTls,
                cancellationToken);
            await smtp.AuthenticateAsync(
                senderEmail, appPassword, cancellationToken);
            await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);

            return true;
        }
        catch (Exception)
        {
            // Details could reveal the mail settings, so they are not sent
            // to the caller.
            return false;
        }
    }
}
