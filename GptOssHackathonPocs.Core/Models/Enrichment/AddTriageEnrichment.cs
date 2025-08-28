using Microsoft.Extensions.DependencyInjection;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public static class AddTriageEnrichmentExtensions
{
    public static IServiceCollection AddTriageEnrichment(this IServiceCollection services)
    {
        services.AddSingleton<IGeoRegistry, InMemoryGeoRegistry>();
        services.AddSingleton<IAdminResolver, TigerAdminResolver>();
        services.AddSingleton<IPopulationIndex, WorldPopPopulationIndex>();
        services.AddSingleton<ISviIndex, SviIndexArcGis>();
        services.AddSingleton<IFacilityIndex, FacilityIndexHospitals>();
        services.AddSingleton<IncidentCardBuilder>();
        return services;
    }
}
