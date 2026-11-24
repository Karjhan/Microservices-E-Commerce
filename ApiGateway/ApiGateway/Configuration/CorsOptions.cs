namespace ApiGateway.Configuration;

public class CorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "Storefront";
    public string[] AllowedOrigins {get; set;} = [];
}