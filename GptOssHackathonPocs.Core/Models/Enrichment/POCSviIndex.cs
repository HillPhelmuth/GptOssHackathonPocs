using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public sealed class POCSviIndex : ISviIndex
{
    public Task<double> GetSviPercentile(Geometry? g) => Task.FromResult(0.5); // neutral until wired to real SVI
}
