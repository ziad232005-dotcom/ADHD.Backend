namespace ADHD.Backend.Shadows;

public enum ShadowStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>One Shadow Teacher who applied to be listed.</summary>
public sealed class ShadowRecord
{
    public int Id { get; set; }
    public ShadowStatus Status { get; set; } = ShadowStatus.Pending;
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public int YearsExperience { get; set; }
    public List<string> Focus { get; set; } = new();
    public string Education { get; set; } = "";
    public string Availability { get; set; } = "";
    public string Bio { get; set; } = "";
    public int? PricePerSession { get; set; }
    public string? Phone { get; set; }
    public string? Whatsapp { get; set; }
    public string? Email { get; set; }
    public string CertificateFile { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }
}
