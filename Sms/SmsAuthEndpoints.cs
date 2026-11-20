using ADHD.Backend.Auth;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ADHD.Backend.Sms;

public sealed record SendSmsOtpRequest(string Phone);

public sealed record VerifySmsOtpRequest(string Phone, string Code);

public static class SmsAuthEndpoints
{
    public static void MapSmsAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/send-sms-otp", SendSmsOtpAsync)
            .RequireRateLimiting(RateLimitPolicies.Send);

        app.MapPost("/api/auth/verify-sms-otp", VerifySmsOtp)
            .RequireRateLimiting(RateLimitPolicies.Verify);
    }

    private static async Task<IResult> SendSmsOtpAsync(
        SendSmsOtpRequest request,
        OtpService otpService,
        ISmsSender smsSender,
        IOptions<SmsGatewayOptions> options,
        CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryNormalize(request.Phone, out var phone))
        {
            return Results.BadRequest(new
            {
                message = "Please enter a valid mobile number."
            });
        }

        if (!options.Value.AllowedPrefixes.Any(
                prefix => phone.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return Results.BadRequest(new
            {
                message = "SMS verification is only available for " +
                          "Egyptian numbers right now."
            });
        }

        if (!otpService.CanSend(phone, out var retryAfter))
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

        var code = otpService.Issue(phone);

        var sent = await smsSender.SendAsync(
            phone,
            $"Your MAZID verification code is: {code}. " +
            "It expires in 5 minutes.",
            cancellationToken);

        if (!sent)
        {
            otpService.Discard(phone);

            return Results.Problem(
                detail: "Could not send the verification SMS. " +
                        "Please try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new
        {
            message = "Verification code sent successfully."
        });
    }

    private static IResult VerifySmsOtp(
        VerifySmsOtpRequest request,
        OtpService otpService)
    {
        var code = request.Code ?? string.Empty;

        if (!PhoneNumber.TryNormalize(request.Phone, out var phone) ||
            code.Length != 6 ||
            !code.All(char.IsAsciiDigit))
        {
            return Results.BadRequest(new
            {
                message = "Invalid phone number or verification code."
            });
        }

        return otpService.Verify(phone, code) switch
        {
            OtpVerifyResult.Verified => Results.Ok(new
            {
                message = "Phone number verified successfully.",
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
}
