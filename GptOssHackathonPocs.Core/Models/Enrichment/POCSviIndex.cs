using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public sealed class POCSviIndex : ISviIndex
{
    public Task<PopulationSvi> GetSviPercentile(Geometry? g) => Task.FromResult(new PopulationSvi(0,0.5)); // neutral until wired to real SVI
}
