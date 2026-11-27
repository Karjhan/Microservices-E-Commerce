using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Security.Audit;
using Shared.Security.Authorization;
using Shared.Security.Problems;
using Shared.Security.Time;

namespace Shared.Security.Configuration;

public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddSharedSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSharedJwtBearer(configuration);
        services.AddAuthorization(options => options.AddAdminOnlyPolicy());

        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, CurrentUser>();

        services.Replace(ServiceDescriptor.Singleton<IAuthorizationMiddlewareResultHandler, AccessProblemHandler>());
        services.TryAddScoped<IRefusalAuditRecorder, RefusalAuditRecorder>();

        return services;
    }

    public static IServiceCollection AddStoreCalendar(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StoreOptions>(configuration.GetSection(StoreOptions.SectionName));
        services.TryAddSingleton<StoreCalendar>();
        return services;
    }
}
