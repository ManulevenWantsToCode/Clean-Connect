namespace Clean_Connect.Application.DTO;

public record WorkerPrivateProfileDto
{
    public Guid WorkerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Contact { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string ServiceType { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public DateTime DateOfBirth { get; init; }
    public decimal HourlyRate { get; init; }
    public bool IsAvailable { get; init; }
}
