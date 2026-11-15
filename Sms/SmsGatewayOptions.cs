namespace ADHD.Backend.Sms;

/// <summary>
/// Settings for the Android phone that sends the SMS messages
/// (the "SMS Gateway for Android" app running in Local Server mode).
/// Keep the real values in User Secrets, never in the repository.
/// </summary>
public sealed class SmsGatewayOptions
{
    public const string SectionName = "SmsGateway";

    /// <summary>Address of the phone, for example http://192.168.1.50:8080.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Username shown in the app's Local Server section.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Password shown in the app's Local Server section.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Phone numbers must start with one of these prefixes. Limiting the
    /// countries protects the SIM balance from international SMS costs.
    /// </summary>
    public string[] AllowedPrefixes { get; set; } = ["+20"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);
}
