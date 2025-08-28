using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public interface IAdminResolver
{
    string[] ResolveAdminAreas(Geometry? g);
}
