namespace Clean_Connect.Application.DTO;

public record WorkerMarketplaceDto
{
    public Guid WorkerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ServiceType { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public double Rating { get; init; }
    public decimal HourlyRate { get; init; }
}
