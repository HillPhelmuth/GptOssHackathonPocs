using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public interface IFacilityIndex
{
    Task<string[]> NearbyFacilities(Geometry? g);
}

