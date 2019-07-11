using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Itinero.Transit.Data;

namespace Itinero.Transit.OtherMode
{
    public class OtherModeCacher : IOtherModeGenerator
    {
        // ReSharper disable once MemberCanBePrivate.Global
        public IOtherModeGenerator Fallback { get; }

        public OtherModeCacher(IOtherModeGenerator fallback)
        {
            Fallback = fallback;
        }


        private Dictionary<(StopId, StopId tos), uint> _cacheSingle =
            new Dictionary<(StopId, StopId tos), uint>();

        public uint TimeBetween(IStop from, IStop to)
        {
            var key = (from.Id, to.Id);
            if (_cacheSingle.ContainsKey(key))
            {
                return _cacheSingle[key];
            }

            var v = Fallback.TimeBetween(@from, to);
            _cacheSingle[key] = v;
            return v;
        }


        private Dictionary<(StopId from, List<StopId> tos),
            Dictionary<StopId, uint>> _cache =
            new Dictionary<(StopId from, List<StopId> tos),
                Dictionary<StopId, uint>>();

        public Dictionary<StopId, uint> TimesBetween(IStop from,
            IEnumerable<IStop> to)
        {
            /**
             * Tricky situation ahead...
             *
             * The 'to'-list of IStops is probably generated with a return yield ala:
             * 
             *
             * var potentialStopsInRange = ...
             * for(var stop in potentialStopsInRange){
             *    if(distanceBetween(stop, target) <= range{
             *         reader.MoveTo(stop);
             *        yield return stop;
             *     }
             * }
             *
             * (e.g. SearchInBox, which uses 'yield return stopSearchEnumerator.Current')
             *
             * In other words, something as ToList would result in:
             * [reader, reader, reader, ... , reader], which points internally to the same stop
             *
             * But we want all the ids to be able to cache.
             * And we cant use the 'to'-list as cache key, because it points towards the same reader n-times.
             *
             */


            // FIRST select the IDs to make sure 'return yield'-enumerators run correctly on an enumerating object
            to = to.Select(stop => new Stop(stop)).ToList();
            var tos = to.Select(stop => stop.Id).ToList();
            var key = (from.Id, tos);
            if (_cache.ContainsKey(key))
            {
                return _cache[key];
            }

            var v = Fallback.TimesBetween(@from, to);
            _cache[key] = v;
            return v;
        }

        private bool CachingIsDone = false;

        // ReSharper disable once UnusedMember.Global
        public OtherModeCacher PreCalculateCache(IStopsReader withCache)
        {
            // ReSharper disable once RedundantArgumentDefaultValue
            PreCalculateCache(withCache, 0, 0);
            CachingIsDone = true;
            return this;
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public void PreCalculateCache(IStopsReader withCache, int offset = 0, int skiprate = 0)
        {
            withCache.Reset();

            for (var i = 0; i < offset; i++)
            {
                withCache.MoveNext();
            }

            while (withCache.MoveNext())
            {
                var current = (IStop) withCache;
                var inRange = withCache.LocationsInRange(
                    current.Latitude, current.Longitude,
                    Range());
                TimesBetween(withCache, inRange);

                for (var i = 0; i < skiprate; i++)
                {
                    withCache.MoveNext();
                }
            }
        }

        // ReSharper disable once UnusedMember.Global
        public bool ChachingIsDone()
        {
            return CachingIsDone;
        }

        private void PreCalculateCacheMultiThreaded(Func<IStopsReader> stopsReaderGenerator)
        {
            // TODO TEST ME
            var processors = Environment.ProcessorCount;
            var allCaches = new List<OtherModeCacher>();
            var taskPool = new Task[processors];

            for (var i = 0; i < processors; i++)
            {
                var otherCache = new OtherModeCacher(Fallback);
                allCaches.Add(otherCache);

                var task = new Task(() =>
                {
                    var stopsReader = stopsReaderGenerator();
                    otherCache.PreCalculateCache(stopsReader, i, processors);
                });
                taskPool[i] = task;
                task.Start();
            }

            Task.WaitAll(taskPool);

            foreach (var otherModeCacher in allCaches)
            {
                _cache.Union(otherModeCacher._cache);
            }
        }


        public float Range()
        {
            return Fallback.Range();
        }

        public string OtherModeIdentifier()
        {
            return Fallback.OtherModeIdentifier();
        }
    }
}