using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public interface IPopulationIndex
{
    Task<double> EstimatePopulation(Geometry? g);
}
