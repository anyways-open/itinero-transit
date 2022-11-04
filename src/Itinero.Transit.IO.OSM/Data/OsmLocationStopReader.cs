using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Itinero.Transit.Data;
using Itinero.Transit.Data.Attributes;
using Itinero.Transit.Data.Core;
using Itinero.Transit.IO.OSM.Data.Parser;
using Itinero.Transit.Utils;
using static Itinero.Transit.IO.OSM.Data.Parser.DefaultRdParsers;

[assembly: InternalsVisibleTo("Itinero.Transit.Tests")]

namespace Itinero.Transit.IO.OSM.Data
{
    /// <summary>
    /// AN 'OsmLocationStop' is used to represent various floating locations.
    /// As global IDs, we reuse the mapping url:
    /// https://www.openstreetmap.org/#map=[irrelevant_zoom_level]/[lat]/[lon]
    /// E.g.:  https://www.openstreetmap.org/#map=19/51.21575/3.21999
    /// </summary>
    public class OsmLocationStopReader : IStopsReader
    {
        /// <summary>
        /// If we were to search locations close by a given location, we could return infinitely many coordinates.
        ///
        /// However, often we are only interested in finding two locations:
        /// The departure location and the arrival location.
        ///
        /// This list acts as the items that can be returned in 'SearchInBox', searchClosest, etc...
        ///
        /// We expect this list to stay small (at most 100) so we are not gonna optimize this a lot
        /// 
        /// </summary>
        private readonly List<StopId> _searchableLocations = new List<StopId>();


        private readonly uint _databaseId;
        public string GlobalId { get; private set; }
        public StopId Id { get; private set; }
        public double Longitude { get; private set; }
        public double Latitude { get; private set; }
        public IAttributeCollection Attributes => null; //No attributes supported here

        private const uint _precision = 1000000;

        /// <summary>
        /// If enabled, every URL that is decoded will be kept in reachable locations
        /// </summary>
        private readonly bool _hoard;

        /// <summary>
        /// Creates a StopsReader which is capable of decoding OSM-urls
        /// </summary>
        /// <param name="databaseId"></param>
        /// <param name="hoard">If enabled, every decoded URL will be kept as searchableLocation</param>
        public OsmLocationStopReader(uint databaseId, bool hoard = false)
        {
            _databaseId = databaseId;
            _hoard = hoard;
        }

        public bool MoveTo((double latitude, double longitude) location)
        {
            var (lat, lon) =
                location;
            // Slight abuse of the LocationId
            Id = new StopId(_databaseId, (uint) ((lat + 90.0) * _precision), (uint) ((lon + 180) * _precision));
            Latitude = (double) Id.LocalTileId / _precision - 90.0;
            Longitude = (double) Id.LocalId / _precision - 180.0;
            GlobalId = FormattableString.Invariant($"https://www.openstreetmap.org/#map=19/{Latitude}/{Longitude}");
            return true;
        }

        public bool MoveTo(string globalId)
        {
            var result =
                ParseOsmUrl.ParseUrl().Parse((globalId, 0));

            if (!result.Success() || !string.IsNullOrEmpty(result.Rest))
            {
                return false;
            }

            var (lat, lon) = result.Result;
            // Slight abuse of the LocationId
            Id = new StopId(_databaseId, (uint) ((lat + 90.0) * _precision), (uint) ((lon + 180) * _precision));
            Latitude = (double) Id.LocalTileId / _precision - 90.0;
            Longitude = (double) Id.LocalId / _precision - 180.0;
            GlobalId =  FormattableString.Invariant($"https://www.openstreetmap.org/#map=19/{Latitude}/{Longitude}");
            if (_hoard)
            {
                AddSearchableLocation(Id);
            }

            return true;
        }

        public bool MoveTo(StopId stop)
        {
            if (stop.DatabaseId != _databaseId)
            {
                return false;
            }

            Latitude = (double) stop.LocalTileId / _precision - 90.0;
            Longitude = (double) stop.LocalId / _precision - 180.0;
            GlobalId =  FormattableString.Invariant($"https://www.openstreetmap.org/#map=19/{Latitude}/{Longitude}");
            Id = stop;
            return true;
        }

        /// <summary>
        /// Enumerates the special 'inject locations' list
        /// </summary>
        private int _index;

        public HashSet<uint> DatabaseIndexes()
        {
            return new HashSet<uint>() {_databaseId};
        }

        public bool MoveNext()
        {
            _index++;
            if (_index >= _searchableLocations.Count)
            {
                return false;
            }

            MoveTo(_searchableLocations[_index]);
            return true;
        }


        public void Reset()
        {
            _index = 0;
        }

        public void AddSearchableLocation(StopId location)
        {
            if (_searchableLocations.Contains(location))
            {
                // Already there...
                return;
            }

            _searchableLocations.Add(location);
        }

        public StopId AddSearchableLocation((double latitude, double longitude) location)
        {
            MoveTo(location);
            AddSearchableLocation(Id);
            return Id;
        }

        public IEnumerable<IStop> SearchInBox((double minLon, double minLat, double maxLon, double maxLat) box)
        {
            var results = new List<IStop>();
            foreach (var location in _searchableLocations)
            {
                MoveTo(location);
                if (box.minLon <= Longitude && Longitude <= box.maxLon
                                            && box.minLat <= Latitude && Latitude <= box.maxLat)
                {
                    results.Add(new Stop(this));
                }
            }

            return results;
        }


        public IEnumerable<Stop> StopsAround(Stop stop, uint range)
        {
            foreach (var location in _searchableLocations)
            {
                MoveTo(location);
                if (!(DistanceEstimate.DistanceEstimateInMeter(stop.Latitude, stop.Longitude, Latitude, Longitude) <=
                      range))
                {
                    continue;
                }

                if (location.Equals(stop.Id))
                {
                    continue;
                }

                yield return new Stop(this);
            }
        }
    }

    internal static class ParseOsmUrl
    {
        internal static RDParser<int> ParsePrefix()
        {
            return !(LitCI("https") | LitCI("http"))
                   * !Lit("://")
                   * !(LitCI("www.") | Lit(""))
                   * !(LitCI("openstreetmap.org") | LitCI("osm.org") | LitCI("openstreetmap.com"))
                   * !LitCI("/#map=")
                   * Int()
                   + !Lit("/");
        }

        public static RDParser<(double, double)> ParseUrl()
        {
            return RDParser<(double latitude, double longitude)>.X(
                !ParsePrefix() * Double(),
                !Lit("/") *
                Double()
            );
        }
    }
}