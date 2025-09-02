using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public interface IFacilityIndex
{
    Task<NearbyHospital[]> NearbyFacilities(Geometry? g);
}

