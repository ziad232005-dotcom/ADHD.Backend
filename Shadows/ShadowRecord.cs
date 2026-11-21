namespace ADHD.Backend.Shadows;

public enum ShadowStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>One document the teacher uploaded (a certificate).</summary>
public sealed class ShadowDocumentRecord
{
    public string Kind { get; set; } = "";
    public string FileName { get; set; } = "";
    public string CertificateName { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Number { get; set; } = "";
    public DateTime? ObtainedOn { get; set; }

    /// <summary>Name of the stored file inside the certificates folder.</summary>
    public string StoredFile { get; set; } = "";
    public string ContentType { get; set; } = "";
}

/// <summary>One Shadow Teacher who applied to be listed.</summary>
public sealed class ShadowRecord
{
    public int Id { get; set; }
    public ShadowStatus Status { get; set; } = ShadowStatus.Pending;

    public string Name { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public string Mobile { get; set; } = "";
    public string Email { get; set; } = "";
    public string Governorate { get; set; } = "";
    public string? PhotoFile { get; set; }

    public string Qualification { get; set; } = "";
    public string University { get; set; } = "";
    public string Faculty { get; set; } = "";
    public string Department { get; set; } = "";
    public string GraduationYear { get; set; } = "";

    public string YearsExperience { get; set; } = "";
    public string CurrentWorkplace { get; set; } = "";
    public string PreviousWorkplaces { get; set; } = "";
    public string ChildExperience { get; set; } = "";
    public string AdhdExperience { get; set; } = "";
    public List<string> Skills { get; set; } = new();

    public List<ShadowDocumentRecord> Documents { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }
}
