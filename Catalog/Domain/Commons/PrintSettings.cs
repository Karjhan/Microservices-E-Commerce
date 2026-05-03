namespace Domain.Commons;

public class PrintSettings
{
    public decimal? LayerHeight { get; private set; }  
    public int? InfillPercentage { get; private set; } 
    public int? NozzleSize { get; private set; }    
    public int? PrintTimeMinutes { get; private set; }
    public decimal? FilamentUsedGrams { get; private set; }

    public bool SupportsRequired { get; private set; }
}