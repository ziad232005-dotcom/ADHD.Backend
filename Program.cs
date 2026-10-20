using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Temporary storage for local testing.
// OTP expires after 5 minutes.
var otpStorage =
    new ConcurrentDictionary<string, (string CodeHash, DateTime ExpiresAt)>(
        StringComparer.OrdinalIgnoreCase
    );

app.MapGet("/", () => Results.Ok(new
{
    message = "MAZID Backend is running"
}));

// Send OTP
app.MapPost("/api/auth/send-otp", async (
    SendOtpRequest request,
    IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) ||
        !System.Net.Mail.MailAddress.TryCreate(
            request.Email.Trim(), out var address) ||
        !string.Equals(
            address.Address,
            request.Email.Trim(),
            StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new
        {
            message = "Please enter a valid email address."
        });
    }

    var senderEmail = configuration["EmailSettings:SenderEmail"];
    var appPassword = configuration["EmailSettings:AppPassword"];
    var smtpServer = configuration["EmailSettings:SmtpServer"];

    if (string.IsNullOrWhiteSpace(senderEmail) ||
        string.IsNullOrWhiteSpace(appPassword) ||
        string.IsNullOrWhiteSpace(smtpServer))
    {
        return Results.Problem(
            "Email settings are missing. Check your local User Secrets.");
    }

    var email = address.Address;
    var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    var message = new MimeMessage();
    message.From.Add(new MailboxAddress("MAZID", senderEmail));
    message.To.Add(MailboxAddress.Parse(email));
    message.Subject = "Your MAZID Verification Code";

    message.Body = new TextPart("plain")
    {
        Text =
            $"Your MAZID verification code is: {otp}\n\n" +
            "This code expires in 5 minutes.\n\n" +
            "If you did not request this code, please ignore this email."
    };

    try
    {
        using var smtp = new SmtpClient();

        await smtp.ConnectAsync(
            smtpServer,
            587,
            SecureSocketOptions.StartTls);

        await smtp.AuthenticateAsync(senderEmail, appPassword);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);

        // Store only the hash of the OTP.
        var codeHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(otp)));

        otpStorage[email] = (
            codeHash,
            DateTime.UtcNow.AddMinutes(5));

        return Results.Ok(new
        {
            message = "Verification code sent successfully."
        });
    }
    catch (Exception)
    {
        return Results.Problem(
            "Could not send the verification email. Please try again.");
    }
});

// Verify OTP
app.MapPost("/api/auth/verify-otp", (VerifyOtpRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Code) ||
        !System.Net.Mail.MailAddress.TryCreate(
            request.Email.Trim(), out var address) ||
        !string.Equals(
            address.Address,
            request.Email.Trim(),
            StringComparison.OrdinalIgnoreCase) ||
        request.Code.Length != 6 ||
        request.Code.Any(c => c < '0' || c > '9'))
    {
        return Results.BadRequest(new
        {
            message = "Invalid email or verification code."
        });
    }

    var email = address.Address;

    if (!otpStorage.TryGetValue(email, out var storedOtp))
    {
        return Results.BadRequest(new
        {
            message = "No active verification code. Request a new code."
        });
    }

    if (DateTime.UtcNow > storedOtp.ExpiresAt)
    {
        otpStorage.TryRemove(email, out _);

        return Results.BadRequest(new
        {
            message = "Verification code expired. Request a new code."
        });
    }

    var submittedHash = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(request.Code)));

    if (!CryptographicOperations.FixedTimeEquals(
        Convert.FromHexString(submittedHash),
        Convert.FromHexString(storedOtp.CodeHash)))
    {
        return Results.BadRequest(new
        {
            message = "Incorrect verification code."
        });
    }

    // Remove the code after successful verification.
    otpStorage.TryRemove(email, out _);

    return Results.Ok(new
    {
        message = "Email verified successfully.",
        verified = true
    });
});

app.Run();

record SendOtpRequest(string Email);
record VerifyOtpRequest(string Email, string Code);

