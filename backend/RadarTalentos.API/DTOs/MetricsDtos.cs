namespace RadarTalentos.API.DTOs;

public record RateDto(int? Rate, int Count, int Total);
public record FunnelStep(string Stage, int Count, int? Conversion);
public record SourceMetric(string Source, int Total, int Hired, int Pct);
public record ReasonMetric(string Reason, int Count);
public record OfferVsRequest(int Below, int Compatible, int Above, int Total);

public record MetricsDto(
    int TotalAccessed,
    int Interviewed,
    int OpenJobs,
    int? AvgCloseTimeDays,
    RateDto Acceptance,
    RateDto Decline,
    List<FunnelStep> Funnel,
    List<SourceMetric> Sources,
    List<ReasonMetric> RejectionReasons,
    OfferVsRequest OfferVsRequest);
