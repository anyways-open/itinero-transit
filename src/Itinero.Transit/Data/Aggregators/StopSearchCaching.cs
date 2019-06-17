using System.Collections.Generic;
using System.Linq;
using Itinero.Transit.Data.Attributes;

namespace Itinero.Transit.Data.Aggregators
{
    public static class StopReaderExtensions
    {
        public static IStopsReader UseCache(this IStopsReader stopsReader)
        {
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (stopsReader is StopSearchCaching)
            {
                return stopsReader;
            }

            return new StopSearchCaching(stopsReader);
        }
    }


    public class StopSearchCaching : IStopsReader
    {
        private readonly IStopsReader _stopsReader;

        private Dictionary<(double, double, double, double), IEnumerable<IStop>>
            cache = new Dictionary<(double, double, double, double), IEnumerable<IStop>>();

        public StopSearchCaching(IStopsReader stopsReader)
        {
            _stopsReader = stopsReader;
        }


        public IEnumerable<IStop> SearchInBox((double minLon, double minLat, double maxLon, double maxLat) box)
        {
            if (cache.ContainsKey(box))
            {
                return cache[box];
            }

            var v = _stopsReader.SearchInBox(box).ToList();
            cache[box] = v;
            return v;
        }


        // ----------- Only boring, generated code below ------------ //        


        public string GlobalId => _stopsReader.GlobalId;

        public LocationId Id => _stopsReader.Id;

        public double Longitude => _stopsReader.Longitude;

        public double Latitude => _stopsReader.Latitude;

        public IAttributeCollection Attributes => _stopsReader.Attributes;

        public bool MoveTo(LocationId stop)
        {
            return _stopsReader.MoveTo(stop);
        }

        public bool MoveTo(string globalId)
        {
            return _stopsReader.MoveTo(globalId);
        }

        public bool MoveNext()
        {
            return _stopsReader.MoveNext();
        }

        public void Reset()
        {
            _stopsReader.Reset();
        }
    }
}