using System.Text.Json;

namespace ADHD.Backend.Shadows;

/// <summary>
/// Keeps the applications in a JSON file and the certificates next to it.
/// Good enough for a graduation project; a database replaces it later.
/// </summary>
public sealed class ShadowStore
{
    public const int MaxApplications = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _lock = new();
    private readonly string _folder;
    private readonly string _certificates;
    private readonly string _file;
    private List<ShadowRecord> _items;

    public ShadowStore(IConfiguration configuration)
    {
        _folder = Path.GetFullPath(
            configuration["Shadows:DataPath"] ?? "App_Data");
        _certificates = Path.Combine(_folder, "certificates");
        _file = Path.Combine(_folder, "shadows.json");

        Directory.CreateDirectory(_certificates);
        _items = Load();
    }

    private List<ShadowRecord> Load()
    {
        if (!File.Exists(_file)) return new List<ShadowRecord>();

        try
        {
            return JsonSerializer.Deserialize<List<ShadowRecord>>(
                File.ReadAllText(_file)) ?? new List<ShadowRecord>();
        }
        catch (JsonException)
        {
            return new List<ShadowRecord>();
        }
    }

    private void Save()
    {
        // Write to a temporary file first so a crash cannot leave half a file.
        var temp = _file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_items, JsonOptions));
        File.Move(temp, _file, overwrite: true);
    }

    /// <summary>Returns the new record, or null when the store is full.</summary>
    public ShadowRecord? Add(ValidatedApplication application)
    {
        lock (_lock)
        {
            if (_items.Count >= MaxApplications) return null;

            var record = application.Record;
            record.Id = _items.Count == 0 ? 1 : _items.Max(i => i.Id) + 1;
            record.CreatedAt = DateTime.UtcNow;
            record.Status = ShadowStatus.Pending;

            if (application.Photo is { } photo)
            {
                record.PhotoFile = $"{record.Id}_photo{photo.Extension}";
                File.WriteAllBytes(
                    Path.Combine(_certificates, record.PhotoFile), photo.Bytes);
            }

            var number = 0;
            foreach (var (info, file) in application.Documents)
            {
                info.StoredFile = $"{record.Id}_{number++}{file.Extension}";
                File.WriteAllBytes(
                    Path.Combine(_certificates, info.StoredFile), file.Bytes);
                record.Documents.Add(info);
            }

            _items.Add(record);
            Save();
            return record;
        }
    }

    public List<ShadowRecord> WithStatus(ShadowStatus status)
    {
        lock (_lock)
        {
            return _items
                .Where(i => i.Status == status)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();
        }
    }

    public ShadowRecord? Find(int id)
    {
        lock (_lock)
        {
            return _items.FirstOrDefault(i => i.Id == id);
        }
    }

    public bool Review(int id, ShadowStatus status, string? reason)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item is null) return false;

            item.Status = status;
            item.ReviewedAt = DateTime.UtcNow;
            item.RejectReason = status == ShadowStatus.Rejected ? reason : null;
            Save();
            return true;
        }
    }

    /// <summary>One stored document of [id], or null.</summary>
    public (string Path, string ContentType, string Name)? Document(int id, int index)
    {
        ShadowRecord? item;
        lock (_lock)
        {
            item = _items.FirstOrDefault(i => i.Id == id);
        }
        if (item is null || index < 0 || index >= item.Documents.Count) return null;

        var doc = item.Documents[index];
        var path = Path.Combine(_certificates, doc.StoredFile);
        return File.Exists(path) ? (path, doc.ContentType, doc.FileName) : null;
    }

    /// <summary>The profile photo of [id], or null.</summary>
    public (string Path, string ContentType)? Photo(int id)
    {
        ShadowRecord? item;
        lock (_lock)
        {
            item = _items.FirstOrDefault(i => i.Id == id);
        }
        if (item?.PhotoFile is null) return null;

        var path = Path.Combine(_certificates, item.PhotoFile);
        if (!File.Exists(path)) return null;

        return (path, item.PhotoFile.EndsWith(".png") ? "image/png" : "image/jpeg");
    }
}
