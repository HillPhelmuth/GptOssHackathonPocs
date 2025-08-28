using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.IO.Esri;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public class TigerAdminResolver : IAdminResolver
{
    private readonly STRtree<(Geometry geom, string fips)> _idx = new();
    private const string ShapeFilePath = @"C:\Users\adamh\Downloads\tl_2024_us_county\tl_2024_us_county.shp";
    public TigerAdminResolver()  // county or tract file
    {
        foreach (var feature in Shapefile.ReadAllFeatures(ShapeFilePath))
        {
            var geom = feature.Geometry;               // NTS Geometry
            var attrs = feature.Attributes;      
            var fips = attrs["GEOID"].ToString(); // county/tract GEOID
            _idx.Insert(geom.EnvelopeInternal, (geom, fips)!);
        }
        _idx.Build();
       
    }
    //ToDo NOT WORKING
    public string[] ResolveAdminAreas(Geometry? g)
    {
        if (g is null || g.IsEmpty) return [];
        var hits = _idx.Query(g.EnvelopeInternal)
            .Where(t => t.geom.Intersects(g))
            .Select(t => t.fips)
            .Distinct()
            .ToArray();
        return hits;
    }
}