using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public interface ISviIndex
{
    Task<double> GetSviPercentile(Geometry? g);
} // 0..1
