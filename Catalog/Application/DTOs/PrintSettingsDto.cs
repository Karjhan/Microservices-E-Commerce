namespace Application.DTOs;

public class PrintSettingsDto
{
    public decimal? LayerHeight { get; init; }
    public int? InfillPercentage { get; init; }
    public int? NozzleSize { get; init; }
    public int? PrintTimeMinutes { get; init; }
    public decimal? FilamentUsedGrams { get; init; }
    public bool SupportsRequired { get; init; }
}