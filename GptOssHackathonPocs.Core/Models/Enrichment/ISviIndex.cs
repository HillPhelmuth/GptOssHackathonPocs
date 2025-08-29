using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public interface ISviIndex
{
    Task<PopulationSvi> GetSviPercentile(Geometry? g);
} // 0..1
