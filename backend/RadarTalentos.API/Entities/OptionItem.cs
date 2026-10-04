namespace RadarTalentos.API.Entities;

public static class OptionCategories
{
    public const string Diversity = "Diversity";
    public const string Restriction = "Restriction";
    public const string Source = "Source";
    public const string RejectionReason = "RejectionReason";
    public static readonly string[] All = [Diversity, Restriction, Source, RejectionReason];
}

/// <summary>Vocabulários controlados, geridos pelo Admin (D7).</summary>
public class OptionItem
{
    public Guid Id { get; set; }
    public string Category { get; set; } = "";
    public string Value { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
