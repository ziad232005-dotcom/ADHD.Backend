using System.Text.RegularExpressions;

namespace ADHD.Backend.Shadows;

public sealed record ShadowDocumentRequest(
    string? Kind,
    string? FileName,
    string? CertificateName,
    string? Issuer,
    string? Number,
    DateTime? ObtainedOn,
    string? Data);

public sealed record ShadowApplicationRequest(
    string? FullName,
    DateTime? BirthDate,
    string? Mobile,
    string? Email,
    string? Governorate,
    string? Qualification,
    string? University,
    string? Faculty,
    string? Department,
    string? GraduationYear,
    string? YearsExperience,
    string? CurrentWorkplace,
    string? PreviousWorkplaces,
    string? ChildExperience,
    string? AdhdExperience,
    List<string>? Skills,
    string? Photo,
    List<ShadowDocumentRequest>? Documents);

/// <summary>A file ready to be stored, with its details.</summary>
public sealed record UploadedFile(
    byte[] Bytes,
    string Extension,
    string ContentType);

/// <summary>What a valid application turns into.</summary>
public sealed record ValidatedApplication(
    ShadowRecord Record,
    UploadedFile? Photo,
    List<(ShadowDocumentRecord Info, UploadedFile File)> Documents);

/// <summary>Checks an application and turns it into a record.</summary>
public static class ShadowValidation
{
    public static readonly string[] SkillOptions =
    {
        "Behavior support", "Visual schedules", "Sensory regulation",
        "Parent communication", "Social skills", "Focus training"
    };

    public static readonly string[] Qualifications =
    {
        "Diploma", "Bachelor’s degree", "Master’s degree", "PhD"
    };

    public static readonly string[] DocumentKinds =
    {
        "degree", "special_education", "adhd_training",
        "behavior_management", "child_protection", "experience"
    };

    public const int MaxDocumentBytes = 10 * 1024 * 1024;
    public const int MaxTotalBytes = 18 * 1024 * 1024;
    public const int MaxPhotoBytes = 2 * 1024 * 1024;

    private static readonly Regex PersonName = new(
        @"^[\p{L}\p{M}]+( [\p{L}\p{M}]+)*$", RegexOptions.Compiled);

    private static readonly Regex Email = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    /// <summary>Returns an error message, or null and the result.</summary>
    public static string? Validate(
        ShadowApplicationRequest r,
        out ValidatedApplication? result)
    {
        result = null;

        var name = r.FullName?.Trim() ?? "";
        if (name.Length < 2 || name.Length > 80 || !PersonName.IsMatch(name))
            return "The name can only contain letters.";

        if (r.BirthDate is null) return "Please enter the date of birth.";
        var age = (int)((DateTime.UtcNow - r.BirthDate.Value).TotalDays / 365.25);
        if (age < 18 || age > 80) return "The date of birth is not valid.";

        var mobile = CleanPhone(r.Mobile);
        if (mobile is null) return "The mobile number is not valid.";

        var email = r.Email?.Trim() ?? "";
        if (email.Length > 120 || !Email.IsMatch(email))
            return "The email is not valid.";

        var governorate = Required(r.Governorate, 40);
        if (governorate is null) return "Please choose the governorate.";

        var qualification = r.Qualification?.Trim() ?? "";
        if (!Qualifications.Contains(qualification))
            return "Please choose the qualification.";

        var graduationYear = r.GraduationYear?.Trim() ?? "";
        if (!Regex.IsMatch(graduationYear, @"^(19|20)\d{2}$"))
            return "The graduation year is not valid.";

        var years = Required(r.YearsExperience, 20);
        if (years is null) return "Please enter the years of experience.";

        var skills = (r.Skills ?? new List<string>())
            .Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().ToList();
        // Teachers may add their own skills; only the length is limited.
        if (skills.Count > 15 || skills.Any(s => s.Length > 40))
            return "Too many skills, or a skill name is too long.";

        var record = new ShadowRecord
        {
            Name = name,
            BirthDate = r.BirthDate.Value,
            Mobile = mobile,
            Email = email,
            Governorate = governorate,
            Qualification = qualification,
            University = Optional(r.University, 120),
            Faculty = Optional(r.Faculty, 120),
            Department = Optional(r.Department, 120),
            GraduationYear = graduationYear,
            YearsExperience = years,
            CurrentWorkplace = Optional(r.CurrentWorkplace, 120),
            PreviousWorkplaces = Optional(r.PreviousWorkplaces, 300),
            ChildExperience = Optional(r.ChildExperience, 40),
            AdhdExperience = Optional(r.AdhdExperience, 40),
            Skills = skills
        };

        UploadedFile? photo = null;
        if (!string.IsNullOrEmpty(r.Photo))
        {
            var photoError = ReadFile(r.Photo, MaxPhotoBytes, false, out photo);
            if (photoError is not null) return "The profile photo is not valid.";
        }

        var documents = new List<(ShadowDocumentRecord, UploadedFile)>();
        long total = 0;
        foreach (var d in r.Documents ?? new List<ShadowDocumentRequest>())
        {
            var kind = d.Kind?.Trim() ?? "";
            if (!DocumentKinds.Contains(kind) ||
                documents.Any(x => x.Item1.Kind == kind))
                return "A document type is not valid.";

            var error = ReadFile(d.Data, MaxDocumentBytes, true, out var file);
            if (error is not null || file is null)
                return error ?? "A document is not valid.";

            total += file.Bytes.Length;
            if (total > MaxTotalBytes) return "The documents are too large.";

            documents.Add((new ShadowDocumentRecord
            {
                Kind = kind,
                FileName = Optional(d.FileName, 120),
                CertificateName = Optional(d.CertificateName, 120),
                Issuer = Optional(d.Issuer, 120),
                Number = Optional(d.Number, 60),
                ObtainedOn = d.ObtainedOn,
                ContentType = file.ContentType
            }, file));
        }

        if (!documents.Any(x => x.Item1.Kind == "degree"))
            return "Upload your university degree certificate.";

        result = new ValidatedApplication(record, photo, documents);
        return null;
    }

    private static string? Required(string? value, int max)
    {
        var text = value?.Trim() ?? "";
        return text.Length == 0 || text.Length > max ? null : text;
    }

    private static string Optional(string? value, int max)
    {
        var text = value?.Trim() ?? "";
        return text.Length > max ? text[..max] : text;
    }

    private static string? CleanPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        var digits = trimmed.Count(char.IsDigit);
        var allowed = trimmed.All(c => char.IsDigit(c) || c is '+' or ' ' or '-');
        return allowed && digits is >= 8 and <= 15 ? trimmed : null;
    }

    /// <summary>
    /// Decodes a file and checks its first bytes, not its name, so only real
    /// PDF, JPG and PNG files are stored.
    /// </summary>
    private static string? ReadFile(
        string? base64,
        int maxBytes,
        bool allowPdf,
        out UploadedFile? file)
    {
        file = null;
        if (string.IsNullOrEmpty(base64)) return "A file is missing.";

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return "A file is not valid.";
        }

        if (bytes.Length == 0) return "A file is empty.";
        if (bytes.Length > maxBytes) return "A file is too large.";

        if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            file = new UploadedFile(bytes, ".jpg", "image/jpeg");
        else if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 &&
                 bytes[2] == 0x4E && bytes[3] == 0x47)
            file = new UploadedFile(bytes, ".png", "image/png");
        else if (allowPdf && bytes.Length > 5 && bytes[0] == 0x25 &&
                 bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
            file = new UploadedFile(bytes, ".pdf", "application/pdf");
        else
            return allowPdf
                ? "Documents must be PDF, JPG or PNG files."
                : "The photo must be a JPG or PNG file.";

        return null;
    }
}
