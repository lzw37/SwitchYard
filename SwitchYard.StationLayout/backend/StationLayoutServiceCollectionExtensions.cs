using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SwitchYard.StationLayout;

public static class StationLayoutServiceCollectionExtensions
{
    /// <summary>
    /// Registers the host-independent station-layout application service.
    /// The host must separately register IStationLayoutRepository,
    /// IStationLayoutAuthorization, and IStationLayoutIdGenerator.
    /// </summary>
    public static IServiceCollection AddSwitchYardStationLayout(
        this IServiceCollection services,
        Action<StationLayoutModuleOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new StationLayoutModuleOptions();
        configure?.Invoke(options);
        if (string.IsNullOrWhiteSpace(options.DefaultSchemeId))
        {
            throw new ArgumentException("DefaultSchemeId cannot be empty.", nameof(configure));
        }

        if (string.IsNullOrWhiteSpace(options.DefaultSchemeName))
        {
            throw new ArgumentException("DefaultSchemeName cannot be empty.", nameof(configure));
        }

        if (options.MaximumSchemeNameLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configure),
                "MaximumSchemeNameLength must be greater than zero.");
        }

        services.TryAddSingleton(options);
        services.TryAddSingleton<IStationLayoutArtifactSink, NullStationLayoutArtifactSink>();
        services.TryAddScoped<IStationLayoutService, StationLayoutService>();
        return services;
    }
}
