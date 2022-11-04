using System;
using System.Collections.Generic;
using Itinero.Transit.Data.Core;
using Itinero.Transit.Utils;

namespace Itinero.Transit.OtherMode
{
    /// <summary>
    /// Generates walks between transport stops, solely based on the distance between them.
    /// The time needed depends on the given parameter
    ///
    /// </summary>
    public class CrowsFlightTransferGenerator : IOtherModeGenerator
    {
        private readonly uint _maxDistance;
        private readonly float _speed;

        ///  <summary>
        ///  Generates a walk constructor.
        /// 
        ///  A walk will only be generated between two locations iff:
        ///  - The given locations are not the same
        ///  - The given locations are no more then 'maxDistance' away from each other.
        /// 
        ///  The time needed for this transfer is calculated based on
        ///  - the distance between the two locations and
        ///  - the speed parameter
        ///  
        ///  </summary>
        /// <param name="maxDistance">The maximum walkable distance in meter</param>
        ///  <param name="speed">In meter per second. According to Wikipedia, about 1.4m/s is preferred average</param>
        public CrowsFlightTransferGenerator(uint maxDistance = 500, float speed = 1.4f)
        {
            _maxDistance = maxDistance;
            _speed = speed;
        }


        public uint TimeBetween(Stop from, Stop to)
        {
            var distance =
                DistanceEstimate.DistanceEstimateInMeter(
                    (from.Longitude, from.Latitude), (to.Longitude, to.Latitude));
            if (distance > _maxDistance)
            {
                return uint.MaxValue;
            }

            if (from.Equals(to))
            {
                return uint.MaxValue;
            }

            return (uint) (distance * _speed);
        }

        public Dictionary<Stop, uint> TimesBetween(Stop from,
            IEnumerable<Stop> to)
        {
            return this.DefaultTimesBetween(from, to);
        }

        public Dictionary<Stop, uint> TimesBetween(IEnumerable<Stop> from, Stop to)
        {
            return this.DefaultTimesBetween(from, to);
        }


        public uint Range()
        {
            return _maxDistance;
        }

        public string OtherModeIdentifier()
        {
            return FormattableString.Invariant($"crowsflight&maxDistance={_maxDistance}&speed={_speed}");
        }

        public IOtherModeGenerator GetSource(Stop from, Stop to)
        {
            return this;
        }
    }
}