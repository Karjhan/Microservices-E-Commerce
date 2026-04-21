using Auth.Endpoints;

namespace Auth.Extensions;

public static class EndpointExtensions
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        RegisterEndpoint.Map(app);
        LoginEndpoint.Map(app);
        RefreshTokenEndpoint.Map(app);
        GoogleLoginEndpoint.Map(app);
    }
}