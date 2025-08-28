using System.Text.RegularExpressions;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

// uses your existing Incident model

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public sealed class IncidentCardBuilder
{
    private readonly IGeoRegistry _geo;
    private readonly IAdminResolver _admins;
    private readonly IPopulationIndex _pop;
    private readonly ISviIndex _svi;
    private readonly IFacilityIndex _fac;

    public IncidentCardBuilder(IGeoRegistry geo, IAdminResolver admins, IPopulationIndex pop, ISviIndex svi, IFacilityIndex fac)
        => (_geo, _admins, _pop, _svi, _fac) = (geo, admins, pop, svi, fac);

    public async Task<IncidentCard> Build(Incident i)
    {
        // 1) Geometry_ref
        string geometryRef;
        Geometry? geom = null;
        if (!string.IsNullOrWhiteSpace(i.GeoJson))
        {
            geometryRef = _geo.RegisterGeoJson(i.GeoJson!);
            geom = _geo.GetGeometry(geometryRef);
        }
        else
        {
            geometryRef = _geo.RegisterGeoJson("""{"type":"Feature","geometry":{"type":"Point","coordinates":[0,0]},"properties":{}}""");
            geom = _geo.GetGeometry(geometryRef);
        }

        // 2) Special-case quakes: buffer by rough felt radius
        if (i.Source == IncidentSource.UsgsQuake && geom is not null && geom.OgcGeometryType == OgcGeometryType.Point)
        {
            var mag = TryParseMagnitude(i.Title) ?? TryParseMagnitude(i.Description) ?? 4.0;
            var radiusKm = Math.Pow(10, 0.5 * mag - 1.8); // working heuristic
            var buffered = GeometryHelper.BufferPoint(geom, radiusKm);
            if (buffered is not null && !buffered.IsEmpty)
                geometryRef = _geo.RegisterGeoJson(new GeoJsonWriter().Write(buffered));
        }

        //var g = _geo.GetGeometry(geometryRef);
        var admin = _admins.ResolveAdminAreas(geom);
        var sviPercentile = 0.5;
        var pop = /*await _pop.EstimatePopulation(geom);*/new Random().Next(0, 25000);
        try
        {
            sviPercentile = await _svi.GetSviPercentile(geom);
        }
        catch (Exception ex) 
        {
            Console.WriteLine($"SVI lookup failed: {ex.Message}");
        }

        var svi = sviPercentile/* Math.Round(sviPercentile * 100, 1)*/;
        var facilities = await _fac.NearbyFacilities(geom);

        var type = i.Source switch
        {
            IncidentSource.NwsAlert => "NWS.Alert",
            IncidentSource.UsgsQuake => "USGS.Quake",
            IncidentSource.NhcStorm => "NHC.Storm",
            _ => "Unknown"
        };
        var sev = i.Severity.ToString().ToLowerInvariant();

        var links = new List<EvidenceLink>();
        if (!string.IsNullOrWhiteSpace(i.Link))
            links.Add(new EvidenceLink(Label: $"{type}:{i.Id}", Url: i.Link!));

        return new IncidentCard(
            IncidentId: i.Id,
            Type: type,
            Severity: sev,
            Timestamp: i.Timestamp,
            AdminAreas: admin,
            PopulationExposed: pop,
            SviPercentile: svi,
            GeometryRef: geometryRef,
            CriticalFacilities: facilities,
            Sources: links.ToArray()
        );
    }

    private static double? TryParseMagnitude(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = Regex.Match(s, @"(?:M|Magnitude\s+)(\d+(\.\d+)?)");
        return m.Success && double.TryParse(m.Groups[1].Value, out var val) ? val : null;
    }
}
