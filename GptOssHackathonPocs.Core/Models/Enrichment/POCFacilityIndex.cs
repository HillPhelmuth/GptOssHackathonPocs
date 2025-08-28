using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public sealed class POCFacilityIndex : IFacilityIndex
{
    public Task<string[]> NearbyFacilities(Geometry? g) => Task.FromResult(Array.Empty<string>());
}
