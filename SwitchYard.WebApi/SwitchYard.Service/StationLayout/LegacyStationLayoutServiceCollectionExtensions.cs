using Microsoft.Extensions.DependencyInjection.Extensions;
using SwitchYard.StationLayout;

namespace SwitchYard.Service.StationLayout;

public static class LegacyStationLayoutServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared station-layout application layer against SwitchYard's
    /// existing capacity-database, authorization, ID and artifact facilities.
    /// This method intentionally does not map endpoints; the legacy controller
    /// remains the sole public route owner during differential verification.
    /// </summary>
    public static IServiceCollection AddSwitchYardStationLayoutLegacyHost(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IStationLayoutIdGenerator, LegacyStationLayoutIdGenerator>();
        services.TryAddScoped<IStationLayoutRepository, LegacyStationLayoutRepository>();
        services.TryAddScoped<IStationLayoutAuthorization, LegacyStationLayoutAuthorization>();
        services.TryAddSingleton<IStationLayoutArtifactSink, LegacyStationLayoutArtifactSink>();
        services.AddSwitchYardStationLayout(options =>
        {
            options.LegacyV1Compatibility = true;
            options.DefaultSchemeId = "station_layout_scheme";
            options.DefaultSchemeName = "车站布置图";
        });
        return services;
    }
}
