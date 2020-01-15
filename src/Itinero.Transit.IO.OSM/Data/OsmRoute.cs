using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Xml;
using GeoTimeZone;
using Itinero.Transit.IO.OSM.Data.Parser;
using Itinero.Transit.Utils;
using OsmSharp;
using OsmSharp.Complete;
using OsmSharp.Streams;
using OsmSharp.Tags;

[assembly: InternalsVisibleTo("Itinero.Transit.Tests.Functional")]

namespace Itinero.Transit.IO.OSM.Data
{
    /// <summary>
    /// Loads a PTv2-route relation from OSM, and adds it to a transitDB (for a given time range)
    /// </summary>
    internal class OsmRoute
    {
        public readonly List<(string url, double lon, double lat, TagsCollectionBase)> StopPositions;
        public readonly bool RoundTrip;
        public TimeSpan Duration;
        public TimeSpan Interval;
        public readonly long Id;
        public readonly string Name;

        private OsmRoute(CompleteRelation relation)
        {
            Id = relation.Id;


            var ts = relation.Tags;


            ts.TryGetValue("name", out Name);
            Name = Name ?? "";
            ts.TryGetValue("roundtrip", out var rt);
            RoundTrip = (rt ?? "").Equals("yes");

            ts.TryGetValue("duration", out var duration);
            Duration = DefaultRdParsers.Duration().ParseFull(duration, "No or incorrect value for duration");


            ts.TryGetValue("interval", out var interval);
            Interval = interval == null
                ? Duration // Assume one shuttle bus
                : DefaultRdParsers.Duration().ParseFull(interval, "No or incorrect value for interval");

            StopPositions = ExtractStopPositions(relation);

            if (StopPositions.Count == 0)
            {
                throw new ArgumentException("This route does not contain stop positions");
            }
        }


        private static List<(string url, double lon, double lat, TagsCollectionBase)> ExtractStopPositions(CompleteRelation relation)
        {
            var stopPositions = new List<(string,  double lon, double lat, TagsCollectionBase)>();
            // First pass: explicitly located stop positions
            foreach (var member in relation.Members)
            {
                var el = member.Member;

                if (!member.Role.Equals("stop") || !(el is Node node)) continue;


                if (node.Latitude == null || node.Longitude == null)
                {
                    throw new ArgumentNullException();
                }

                var nodeId = $"https://www.openstreetmap.org/node/{node.Id}";
                stopPositions.Add((nodeId, node.Longitude.Value, node.Latitude.Value, el.Tags));
            }


            // Second pass: platforms close to the route
            foreach (var member in relation.Members)
            {
                var el = member.Member;

                el.Tags.TryGetValue("public_transport", out var pt);
                if (!member.Role.Equals("platform") && !"platform".Equals(pt)) continue;
                // This is a public transport object - there might be a stop position here

                double lat, lon;
                string id;


                if (el is Node node)
                {
                    if (node.Latitude == null || node.Longitude == null)
                    {
                        throw new ArgumentException("Got a node without latitude or longitude");
                    }

                    lat = node.Latitude.Value;
                    lon = node.Longitude.Value;
                    id = "node/" + node.Id;
                }
                else
                {
                    throw new ArgumentException("Can not process ways and relations which are a platform yet");
                }


                var nodeId = $"https://www.openstreetmap.org/{id}";

                // We make sure that there is no stop closeby

                var inRange = false;
                foreach (var (_, lon0, lat0, _) in stopPositions)
                {
                    // ReSharper disable once InvertIf
                    if (DistanceEstimate.DistanceEstimateInMeter((lon, lat), (lon0, lat0)) < 25)
                    {
                        inRange = true;
                        break;
                    }
                }

                if (!inRange)
                {
                    stopPositions.Add((nodeId, lon, lat, el.Tags));
                }
            }

            return stopPositions;
        }


        /// <summary>
        /// Gets the timezone of this route.
        /// This is based on the lat/lon of the first stop, which is in turn passed into the GeoTimeZone-package.
        /// This packages uses the OSM-boundaries to determine country and thus timezone
        /// </summary>
        public string GetTimeZone()
        {
            return TimeZoneLookup.GetTimeZone(StopPositions[0].lat, StopPositions[0].lon).Result;
        }


        /// <summary>
        ///  Tries to figure out where the relation is located.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<OsmRoute> LoadFrom(string path)
        {
            // We strip of everything until we only keep the number identifying the relation
            // That, we pass onto the API of OSM

            if (path.StartsWith("https://www.openstreetmap.org/relation/"))
            {
                path = path.Substring("https://www.openstreetmap.org/relation/".Length)
                    .Split('?')[0].Split('#')[0];
            }

            if (path.StartsWith("http://www.openstreetmap.org/relation/"))
            {
                path = path.Substring("http://www.openstreetmap.org/relation/".Length)
                    .Split('?')[0].Split('#')[0];
            }

            if (long.TryParse(path, out _))
            {
                path =
                    $"https://openstreetmap.org/api/0.6/relation/{path}/full";
            }

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (path.StartsWith("http"))
            {
                return LoadFromUrl(new Uri(path));
            }

            // We are probably dealing with a path
            using (var stream = File.OpenRead(path))
            {
                return OsmRouteFromStream(stream);
            }
        }

        private static List<OsmRoute> LoadFromUrl(Uri path)
        {
            var httpClient = new HttpClient();

            var response = httpClient.GetAsync(path).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                throw new WebException($"Could not download {path}");
            }


            using (var fileStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
            {
                try
                {
                    return OsmRouteFromStream(fileStream);
                }
                catch (XmlException e)
                {
                    throw new XmlException(
                        "Trying to load the relation failed. You probably fed a webpage to this method instead of an OSM-relation. Try osm.org/api/0.6/relation/\\{relationId\\}/full instead - or our builtin method",
                        e);
                }
            }
        }


        private static List<OsmRoute> OsmRouteFromStream(Stream fileStream)
        {
            var routes = new List<OsmRoute>();
            var source = new XmlOsmStreamSource(fileStream);
            var stream = source.ToComplete();


            while (stream.MoveNext())
            {
                var cur = stream.Current();

                if (!(cur is CompleteRelation rel))
                {
                    continue;
                }

                if (cur.Tags.ContainsKey("type") && cur.Tags["type"].Equals("route"))
                {
                    routes.Add(new OsmRoute(rel));
                }
            }


            return routes;
        }
    }
}