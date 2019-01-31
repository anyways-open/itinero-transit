using System;
using System.Collections.Generic;
using System.IO;
using Itinero.Transit.Data.Walks;
using Attribute = Itinero.Transit.Data.Attributes.Attribute;

namespace Itinero.Transit.Data
{
    /// <summary>
    /// A transit db contains all connections, trips and stops.
    /// </summary>
    public class TransitDb
    {
        private readonly StopsDb _stopsDb;
        private readonly ConnectionsDb _connectionsDb;
        private readonly TripsDb _tripsDb;

        private readonly Action<TransitDbWriter, DateTime, DateTime> _updateTimeFrame;
        private readonly DateTracker _loadedTimeWindows = new DateTracker();

        /// <summary>
        /// Construct a TransitDb, optionally with a callback to load something in the database
        /// </summary>
        /// <param name="updateTimeFrame">This function should add data via the writer. The writer will be closed when the callback finishes</param>
        public TransitDb(Action<TransitDbWriter, DateTime, DateTime> updateTimeFrame = null)
        {
            _updateTimeFrame = updateTimeFrame;
            _stopsDb = new StopsDb();
            _connectionsDb = new ConnectionsDb();
            _tripsDb = new TripsDb();

            _latestSnapshot = new TransitDbSnapShot(_stopsDb, _tripsDb, _connectionsDb);
        }

        private TransitDb(StopsDb stopsDb, TripsDb tripsDb, ConnectionsDb connectionsDb)
        {
            _stopsDb = stopsDb;
            _connectionsDb = connectionsDb;
            _tripsDb = tripsDb;

            _latestSnapshot = new TransitDbSnapShot(_stopsDb, _tripsDb, _connectionsDb);
        }

        private readonly object _writerLock = new object();
        private TransitDbWriter _writer;
        private TransitDbSnapShot _latestSnapshot;

        /// <summary>
        /// Loads more data into the transitDB, as specified by the callbackfunction.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="refresh">If true, overwrites. If false, only the gaps will be filled</param>
        public void UpdateTimeFrame(DateTime start, DateTime end, bool refresh=false)
        {
            var gaps = new List<(DateTime star, DateTime end)>();
            if (refresh)
            {
                gaps.Add((start, end));
            }
            else
            {
                gaps = _loadedTimeWindows.Gaps(start, end);
            }

            var writer = GetWriter();
            foreach (var (wstart, wend) in gaps)
            {
                _updateTimeFrame.Invoke(writer, wstart, wend);
                _loadedTimeWindows.AddTimeWindow(wstart, wend);
            }

            writer.Close();
        }


        public List<(DateTime start, DateTime end)> LoadedTimeWindows => _loadedTimeWindows.TimeWindows();

        /// <summary>
        /// Gets a writer.
        /// A writer can add or update entries in the database.
        /// Once all updates are done, the writer should be closed to apply the changes.
        /// </summary>
        /// <returns>A writer.</returns>
        /// <exception cref="InvalidOperationException">Throws if there is already a writer active.</exception>
        public TransitDbWriter GetWriter()
        {
            lock (_writerLock)
            {
                if (_writer != null)
                    throw new InvalidOperationException(
                        "There is already a writer active, only one writer per transit db can be active at the same time.");

                _writer = new TransitDbWriter(this);
                return _writer;
            }
        }

        /// <summary>
        /// Gets the latest transit db snapshot.
        /// </summary>
        public TransitDbSnapShot Latest => _latestSnapshot;

        /// <summary>
        /// Reads a transit db an all its data from the given stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The transit db.</returns>
        public static TransitDb ReadFrom(Stream stream)
        {
            var version = stream.ReadByte();
            if (version != 1) throw new InvalidDataException($"Cannot read {nameof(TransitDb)}, invalid version #.");

            var stopsDb = StopsDb.ReadFrom(stream);
            var tripsDb = TripsDb.ReadFrom(stream);
            var connectionsDb = ConnectionsDb.ReadFrom(stream);

            return new TransitDb(stopsDb, tripsDb, connectionsDb);
        }

        /// <summary>
        /// A transit db snapshot, represents a consistent state of the transit db.
        /// </summary>
        public class TransitDbSnapShot
        {
            internal TransitDbSnapShot(StopsDb stopsDb, TripsDb tripsDb, ConnectionsDb connectionsDb)
            {
                StopsDb = stopsDb;
                TripsDb = tripsDb;
                ConnectionsDb = connectionsDb;
            }

            /// <summary>
            /// Gets the stops db.
            /// </summary>
            public StopsDb StopsDb { get; }

            /// <summary>
            /// Gets the trips db.
            /// </summary>
            public TripsDb TripsDb { get; }

            /// <summary>
            /// Gets the connections db.
            /// </summary>
            public ConnectionsDb ConnectionsDb { get; }

            /// <summary>
            /// Copies this transit db to the given stream.
            /// </summary>
            /// <param name="stream">The stream.</param>
            /// <returns>The length of the data written.</returns>
            public long WriteTo(Stream stream)
            {
                var length = 1L;

                byte version = 1;
                stream.WriteByte(version);

                length += StopsDb.WriteTo(stream);
                length += TripsDb.WriteTo(stream);
                length += ConnectionsDb.WriteTo(stream);

                return length;
            }
        }

        /// <summary>
        /// A writer for the transit db.
        /// </summary>
        public class TransitDbWriter
        {
            private readonly TransitDb _parent;
            private readonly StopsDb _stopsDb;
            private readonly ConnectionsDb _connectionsDb;
            private readonly TripsDb _tripsDb;

            internal TransitDbWriter(TransitDb parent)
            {
                _parent = parent;

                _stopsDb = parent._stopsDb.Clone();
                _tripsDb = parent._tripsDb.Clone();
                _connectionsDb = parent._connectionsDb.Clone();
            }

            /// <summary>
            /// Adds or updates a stop.
            /// </summary>
            /// <param name="globalId">The global id.</param>
            /// <param name="longitude">The longitude.</param>
            /// <param name="latitude">The latitude.</param>
            /// <param name="attributes">The attributes.</param>
            /// <returns>The stop id.</returns>
            public (uint tileId, uint localId) AddOrUpdateStop(string globalId, double longitude, double latitude,
                IEnumerable<Attribute> attributes = null)
            {
                var stopsDbReader = _stopsDb.GetReader();
                if (stopsDbReader.MoveTo(globalId))
                {
                    return stopsDbReader.Id;
                }

                return _stopsDb.Add(globalId, longitude, latitude,
                    attributes);
            }

            /// <summary>
            /// Adds or updates a new trip.
            /// </summary>
            /// <param name="globalId">The global id.</param>
            /// <param name="attributes">The attributes.</param>
            /// <returns>The trip id.</returns>
            public uint AddOrUpdateTrip(string globalId, IEnumerable<Attribute> attributes = null)
            {
                var tripsDbReader = _tripsDb.GetReader();
                if (tripsDbReader.MoveTo(globalId))
                {
                    return tripsDbReader.Id;
                }

                return _tripsDb.Add(globalId, attributes);
            }

            /// <summary>
            /// Adds or updates a connection.
            /// </summary>
            /// <param name="globalId">The global id.</param>
            /// <param name="stop1">The first stop.</param>
            /// <param name="stop2">The second stop.</param>
            /// <param name="tripId">The trip id.</param>
            /// <param name="departureTime">The departure time.</param>
            /// <param name="travelTime">The travel time in seconds.</param>
            /// <returns></returns>
            public uint AddOrUpdateConnection((uint localTileId, uint localId) stop1,
                (uint localTileId, uint localId) stop2, string globalId, DateTime departureTime, ushort travelTime,
                uint tripId)
            {
                return _connectionsDb.Add(stop1, stop2, globalId, departureTime, travelTime, tripId);
            }

            /// <summary>
            /// Closes this writer and commits the changes to the transit db.
            /// </summary>
            public void Close()
            {
                var latest = new TransitDbSnapShot(_stopsDb, _tripsDb, _connectionsDb);

                _parent._latestSnapshot = latest;
                _parent._writer = null;
            }
        }
    }
}