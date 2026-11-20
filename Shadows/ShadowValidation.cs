using System.Text.RegularExpressions;

namespace ADHD.Backend.Shadows;

public sealed record ShadowApplicationRequest(
    string? Name,
    string? City,
    int? YearsExperience,
    List<string>? Focus,
    string? Education,
    string? Availability,
    string? Bio,
    int? PricePerSession,
    string? Phone,
    string? Whatsapp,
    string? Email,
    string? Certificate);

/// <summary>Checks an application and turns it into a record.</summary>
public static class ShadowValidation
{
    public static readonly string[] FocusAreas =
    {
        "ADHD", "Autism", "Learning difficulties",
        "Speech delay", "Behavior support"
    };

    private const int MaxCertificateBytes = 1_500_000;

    private static readonly Regex PersonName = new(
        @"^[\p{L}\p{M}]+( [\p{L}\p{M}]+)*$", RegexOptions.Compiled);

    private static readonly Regex Email = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    /// <summary>
    /// Returns an error message, or null and fills [record], [certificate]
    /// and [extension].
    /// </summary>
    public static string? Validate(
        ShadowApplicationRequest r,
        out ShadowRecord record,
        out byte[] certificate,
        out string extension)
    {
        record = new ShadowRecord();
        certificate = Array.Empty<byte>();
        extension = ".jpg";

        var name = r.Name?.Trim() ?? "";
        if (name.Length < 2 || name.Length > 80 || !PersonName.IsMatch(name))
            return "The name can only contain letters.";

        var city = r.City?.Trim() ?? "";
        if (city.Length == 0 || city.Length > 60) return "Please enter the city.";

        if (r.YearsExperience is null or < 0 or > 60)
            return "Years of experience must be from 0 to 60.";

        var focus = (r.Focus ?? new List<string>()).Distinct().ToList();
        if (focus.Count == 0 || focus.Any(f => !FocusAreas.Contains(f)))
            return "Choose the children you work with.";

        var education = r.Education?.Trim() ?? "";
        if (education.Length == 0 || education.Length > 120)
            return "Please enter your degree.";

        var availability = r.Availability?.Trim() ?? "";
        if (availability.Length == 0 || availability.Length > 120)
            return "Please say when you are available.";

        var bio = r.Bio?.Trim() ?? "";
        if (bio.Length < 20 || bio.Length > 500)
            return "The description needs 20 to 500 characters.";

        if (r.PricePerSession is < 0 or > 100000)
            return "The price is not valid.";

        var phone = CleanPhone(r.Phone, out var phoneBad);
        var whatsapp = CleanPhone(r.Whatsapp, out var whatsappBad);
        if (phoneBad || whatsappBad) return "A phone number is not valid.";

        string? email = null;
        if (!string.IsNullOrWhiteSpace(r.Email))
        {
            email = r.Email.Trim();
            if (email.Length > 120 || !Email.IsMatch(email))
                return "The email is not valid.";
        }

        if (phone is null && whatsapp is null && email is null)
            return "Give at least one way to reach you.";

        if (string.IsNullOrEmpty(r.Certificate))
            return "Add a photo of your certificate.";

        try
        {
            certificate = Convert.FromBase64String(r.Certificate);
        }
        catch (FormatException)
        {
            return "The certificate photo is not valid.";
        }

        if (certificate.Length == 0 || certificate.Length > MaxCertificateBytes)
            return "The certificate photo is too large.";

        // Only real pictures: check the first bytes, not the file name.
        if (certificate.Length > 3 &&
            certificate[0] == 0xFF && certificate[1] == 0xD8)
            extension = ".jpg";
        else if (certificate.Length > 8 &&
                 certificate[0] == 0x89 && certificate[1] == 0x50 &&
                 certificate[2] == 0x4E && certificate[3] == 0x47)
            extension = ".png";
        else
            return "The certificate must be a JPG or PNG photo.";

        record = new ShadowRecord
        {
            Name = name,
            City = city,
            YearsExperience = r.YearsExperience.Value,
            Focus = focus,
            Education = education,
            Availability = availability,
            Bio = bio,
            PricePerSession = r.PricePerSession,
            Phone = phone,
            Whatsapp = whatsapp,
            Email = email
        };
        return null;
    }

    private static string? CleanPhone(string? value, out bool bad)
    {
        bad = false;
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        var digits = trimmed.Count(char.IsDigit);
        var onlyAllowed = trimmed.All(c => char.IsDigit(c) || c is '+' or ' ' or '-');
        if (!onlyAllowed || digits < 8 || digits > 15)
        {
            bad = true;
            return null;
        }
        return trimmed;
    }
}
