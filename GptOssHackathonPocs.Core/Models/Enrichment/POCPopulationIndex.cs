using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public sealed class POCPopulationIndex : IPopulationIndex
{
    public Task<double> EstimatePopulation(Geometry? g)
    {
        if (g is null || g.IsEmpty) return Task.FromResult(0.0);
        var env = g.EnvelopeInternal;
        var approxKm2 = Math.Abs(env.Width * env.Height) * (111.0 * 111.0);
        return Task.FromResult(Math.Round(Math.Max(0, approxKm2 * 100))); // toy density 100 ppl/km^2
    }
}
