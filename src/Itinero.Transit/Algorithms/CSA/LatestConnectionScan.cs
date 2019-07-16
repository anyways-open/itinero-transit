﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Itinero.Transit.Algorithms.Filter;
using Itinero.Transit.Data;
using Itinero.Transit.Data.Core;
using Itinero.Transit.Journey;
using Itinero.Transit.OtherMode;
using Itinero.Transit.Utils;

[assembly: InternalsVisibleTo("Itinero.Transit.Tests")]

namespace Itinero.Transit.Algorithms.CSA
{
    /// <summary>
    /// Calculates the fastest journey from A to B arriving at a given time; using CSA (backward A*).
    /// It does _not_ use footpath interlinks (yet)
    /// </summary>
    internal class LatestConnectionScan<T>
        where T : IJourneyMetric<T>
    {
        private readonly List<(StopId, Journey<T>)> _userDepartureLocation;

        private readonly IConnectionEnumerator _connectionsEnumerator;
        private readonly IStopsReader _stopsReader;

        private readonly ulong _earliestDeparture;

        private readonly IOtherModeGenerator _transferPolicy;
        private readonly IOtherModeGenerator _walkPolicy;

        /// <summary>
        /// If a traveller has a hard preference on journeys (e.g. max 5 transfers, no specific combination of stations...),
        /// this can be expressed with the journeyFilter 
        /// </summary>
        private readonly IJourneyFilter<T> _journeyFilter;

        public ulong ScanBeginTime { get; private set; } = ulong.MaxValue;

        public ulong ScanEndTime { get; }

        /// <summary>
        /// Returns the isochrone for this location.
        /// Note that journeys in the isochrone will already be structured in a forward way
        /// (thus: genesis = root, arrival = leaf)
        /// </summary>
        /// <returns></returns>
        public IReadOnlyDictionary<StopId, Journey<T>> Isochrone()
        {
            var reversedJourneys = new Dictionary<StopId, Journey<T>>();
            foreach (var pair in JourneysToArrivalStopTable)
            {
                // Due to the nature of LAS, there can be no choices in the journeys; reversal will only return one value
                var prototype = pair.Value.Reversed()[0];
                reversedJourneys.Add(pair.Key, prototype);
            }

            return reversedJourneys;
        }


        /// <summary>
        /// This dictionary keeps, for each stop, the journey that arrives as late as possible
        /// </summary>
        internal readonly Dictionary<StopId, Journey<T>> JourneysToArrivalStopTable =
            new Dictionary<StopId, Journey<T>>();


        /// <summary>
        /// Keeps track of where we are on each trip, thus if we wouldn't leave a bus once we're on it
        /// </summary>
        private readonly Dictionary<TripId, Journey<T>> _trips = new Dictionary<TripId, Journey<T>>();

        private readonly IConnectionFilter _connectionFilter;


        public LatestConnectionScan(ScanSettings<T> settings)
        {
            _journeyFilter = settings.Profile.JourneyFilter;
            _connectionFilter = settings.Profile.ConnectionFilter;
            // settings.Filter is NOT used and SHOULD NOT BE used


            _stopsReader = settings.StopsReader;
            _earliestDeparture = settings.EarliestDeparture.ToUnixTime();
            ScanEndTime = settings.LastArrival.ToUnixTime();
            _connectionsEnumerator = settings.ConnectionsEnumerator;
            _transferPolicy = settings.Profile.InternalTransferGenerator;
            _userDepartureLocation = settings.DepartureStop;
            _walkPolicy = settings.Profile.WalksGenerator;


            foreach (var (loc, j) in settings.TargetStop)
            {
                var journey = j?.SetTag(Journey<T>.LatestArrivalScanJourney)
                              ?? new Journey<T>(loc, settings.LastArrival.ToUnixTime(),
                                  settings.Profile.MetricFactory,
                                  Journey<T>.LatestArrivalScanJourney);
                JourneysToArrivalStopTable.Add(loc, journey);
                // Allow an walk to end
                WalkTowards(loc);
            }
        }


        /// <summary>
        /// Calculates the journey that arrives as early as possible, as specified by the constructor parameters.
        /// Returns null if no journey could be found;
        ///
        /// Note that running this will, as a side effect, also calculate a profile of what location can be reached with an earliest arrival time.
        /// This can be used to optimize PCS later on.
        /// This profile will have scanned (and thus be reliable) up till the latest scanned departure time.
        ///
        /// In other words, it is important to know when the latest departed connection has left.
        /// - In the case that no route is found, the algorithm will stop with simulating departures after 'lastDeparture' as specified in the ctor
        /// - If a route is found, no departures after the earliest arrival are still calculated, unless...
        /// - ... unless a function 'depArrivalToTimeout' is given. Then, that function can calculate the latest can departure time. This will only be run once the earliest arrival has converged
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Journey<T> CalculateJourney(Func<ulong, ulong, ulong> depArrivalToTimeout = null)
        {
            var enumerator = _connectionsEnumerator;
            enumerator.MoveTo(ScanEndTime);
            if (!enumerator.HasPrevious())
            {
                throw new Exception("Could not calculate the latest arriving country: enumerator is empty");
            }

            var earliestAllowedDeparture = _earliestDeparture;
            Journey<T> bestJourney = null;
            var depleted = false;
            while (!depleted && enumerator.CurrentDateTime >= earliestAllowedDeparture)
            {
                if (!IntegrateBatch())
                {
                    depleted = true;
                }

                // we have reached a new batch of departure times
                // Let's first check if we can reach an end destination already

                /*
                 * if(GetBestTime().bestTime != uint.MinValue){
                 *  -> We found a best route, with a best departure time.
                 *  -> We heighten the 'scan until'-time (earliestAllowedDeparture) to the time we have found
                 *
                 * if(GetBestTime().bestTime == uint.MinValue)
                 *  -> No best route is found yet
                 *  -> we do not update earliestAllowedDeparture.
                 *
                 * The above pseudo code is summarized with:
                 */
                bestJourney = GetBestJourney();
                earliestAllowedDeparture = Math.Max(bestJourney.Time, earliestAllowedDeparture);
            }

            // If we en up here, normally we should have found a route.

            bestJourney = bestJourney ?? GetBestJourney();
            if (bestJourney.Time == ulong.MinValue)
            {
                // Sadly, we didn't find a route within the required time
                ScanBeginTime = earliestAllowedDeparture;
                return null;
            }

            bestJourney = bestJourney.Reversed()[0];
            if (depArrivalToTimeout == null)
            {
                ScanBeginTime = bestJourney.Root.Time;
                return bestJourney;
            }

            // Wait! There is one more thing!
            // The user might need a profile to optimize PCS later on
            // We got an alternative end time, we still calculate a little
            ScanBeginTime = depArrivalToTimeout(bestJourney.Root.Time, bestJourney.Time);
            while (!depleted && enumerator.CurrentDateTime >= ScanBeginTime)
            {
                if (!IntegrateBatch())
                {
                    break;
                }
            }

            return bestJourney;
        }

        /// <summary>
        /// Integrates all connections which happen to have the same departure time.
        /// Once all those connections are handled, the walks from the improved locations are batched
        /// </summary>
        private bool IntegrateBatch()
        {
            var improvedLocations = new List<StopId>();

            var c = new Connection();
            var lastDepartureTime = _connectionsEnumerator.CurrentDateTime;
            bool hasNext;
            do
            {
                _connectionsEnumerator.Current(c);
                var departureImproved = IntegrateConnection(c);

                if (departureImproved)
                {
                    improvedLocations.Add(c.DepartureStop);
                }

                hasNext = _connectionsEnumerator.HasPrevious();
            } while (hasNext && lastDepartureTime == _connectionsEnumerator.CurrentDateTime);

            foreach (var improvedLocation in improvedLocations)
            {
                WalkTowards(improvedLocation);
            }

            return hasNext;
        }


        /// <summary>
        /// Handle a single connection, update the stop positions with new times if possible.
        ///
        /// Returns true if an improvement if to c.DepartureLocation is made
        /// 
        /// </summary>
        /// <param name="c">A DepartureEnumeration, which is used here as if it were a single connection object</param>
        private bool IntegrateConnection(
            Connection c)
        {
            // The connection describes a random connection somewhere
            // Lets check if we can take it

            if (_connectionFilter != null
                && !_connectionFilter.CanBeTaken(c))
            {
                // Filtered away...
                return false;
            }

            var journeyFromArrival = GetJourneyFrom(c.ArrivalStop);


            var trip = c.TripId;
            if (c.ArrivalTime > journeyFromArrival.Time && !_trips.ContainsKey(trip))
            {
                // This connection has already left before we can make it to the stop
                return false;
            }


            Journey<T> journeyFromDeparture;
            if (_trips.ContainsKey(trip))
            {
                // Staying on this trip will take us to our destination
                // We extend the trip journey
                journeyFromDeparture = _trips[trip].ChainBackward(c);
            }
            else if (!c.CanGetOff())
            {
                // We are not on the connection already
                // And we can't get off
                // No use to continue scanning
                return false;
            }
            else
            {
                if (journeyFromArrival.SpecialConnection)
                {
                    // We only insert a transfer before a 'normal' connection
                    journeyFromDeparture = journeyFromArrival.ChainBackward(c);
                }
                else
                {
                    // internal transfer
                    var timeNeeded =
                        _transferPolicy.TimeBetween(_stopsReader, c.ArrivalStop, journeyFromArrival.Location);

                    if (journeyFromArrival.Time - timeNeeded >= c.ArrivalTime)
                    {
                        journeyFromDeparture =
                            journeyFromArrival
                                .ChainBackwardWith(_stopsReader, _transferPolicy, c.ArrivalStop)
                                ?.ChainBackward(c);
                    }
                    else
                    {
                        journeyFromDeparture = null;
                    }
                }
            }

            if (journeyFromDeparture == null)
            {
                // There is no way to get to the destination from this connection
                // Neither by staying seated or getting off at the destination
                return false;
            }

            if (_journeyFilter != null && !_journeyFilter.CanBeTakenBackwards(journeyFromDeparture))
            {
                // The traveller refuses to take this journey
                return false;
            }

            // We can be on this trip, either by getting off or staying seated
            _trips[trip] = journeyFromDeparture;

            // Below this point, we only add it to the journey table...
            // If we can get on at the departureStop that is

            if (!c.CanGetOn())
            {
                return false;
            }

            // At this point, we know that we can take this connection and end up at our destination
            // Only thing left to do: figuring out if this improves the journey from the departure location

            if (!JourneysToArrivalStopTable.ContainsKey(c.DepartureStop))
            {
                JourneysToArrivalStopTable[c.DepartureStop] = journeyFromDeparture;
                return true;
            }

            var oldJourney = JourneysToArrivalStopTable[c.DepartureStop];
            // If the old journey departs later, it is better and we don't overwrite it
            if (journeyFromDeparture.Time <= oldJourney.Time)
            {
                return false;
            }

            JourneysToArrivalStopTable[c.DepartureStop] = journeyFromDeparture;
            return true;
        }


        private void WalkTowards(StopId location)
        {
            if (_walkPolicy == null || _walkPolicy.Range() <= 0f)
            {
                return;
            }

            var journey = JourneysToArrivalStopTable[location];

            foreach (var walkingJourney in journey.WalkTowards(_walkPolicy, _stopsReader))
            {
                var id = walkingJourney.Location;
                if (id.Equals(location))
                {
                    continue;
                }

                if (!JourneysToArrivalStopTable.ContainsKey(id))
                {
                    JourneysToArrivalStopTable[id] = walkingJourney;
                }
                else if (JourneysToArrivalStopTable[id].Time < walkingJourney.Time)
                {
                    // The new journey departs later -> we swap
                    JourneysToArrivalStopTable[id] = walkingJourney;
                }
            }
        }


        /// <summary>
        /// Iterates all the target locations.
        /// Returns the earliest time that one of them can be reached, along with the chosen location.
        /// If no location can be reached, returns 'Time.MinValue'
        /// </summary>
        /// <returns></returns>
        private Journey<T> GetBestJourney()
        {
            var currentBestJourney = Journey<T>.NegativeInfiniteJourney;
            foreach (var (targetLoc, restingJourney) in _userDepartureLocation)
            {
                if (!JourneysToArrivalStopTable.ContainsKey(targetLoc))
                {
                    continue;
                }

                var journey = JourneysToArrivalStopTable[targetLoc].Append(restingJourney);

                if (journey.Time < _earliestDeparture)
                {
                    // Journey departs to early, we skip it
                    continue;
                }

                if (journey.Time > currentBestJourney.Time)
                {
                    currentBestJourney = journey;
                }
            }

            return currentBestJourney;
        }


        private Journey<T>
            GetJourneyFrom(StopId location)
        {
            return JourneysToArrivalStopTable.ContainsKey(location)
                ? JourneysToArrivalStopTable[location]
                : Journey<T>.NegativeInfiniteJourney;
        }
    }
}