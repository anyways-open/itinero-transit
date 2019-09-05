using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using Itinero.Transit.Data.Core;
using Itinero.Transit.Utils;
using Attribute = Itinero.Transit.Data.Attributes.Attribute;

namespace Itinero.Transit.Data
{
    /// <summary>
    /// A transit db contains all connections, trips and stops.
    /// </summary>
    public class TransitDb
    {
        public uint DatabaseId { get; }

        public TransitDb(uint databaseId ) : this(
            new StopsDb(databaseId), new TripsDb(databaseId), new ConnectionsDb(databaseId))
        {
            DatabaseId = databaseId;
        }

        private TransitDb(StopsDb stopsDb, TripsDb tripsDb, ConnectionsDb connectionsDb)
        {
            _latestSnapshot = new TransitDbSnapShot(stopsDb, tripsDb, connectionsDb);
        }

        private readonly object _writerLock = new object();
        private TransitDbWriter _writer;
        private TransitDbSnapShot _latestSnapshot;


        /// <summary>
        /// Gets a writer.
        /// A writer can add or update entries in the database.
        /// Once all updates are done, the writer should be closed to apply the changes.
        /// </summary>
        /// <returns>A writer.</returns>
        /// <exception cref="InvalidOperationException">Throws if there is already a writer active.</exception>
        [Pure]
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
        [Pure]
        public TransitDbSnapShot Latest => _latestSnapshot;


        [Pure]
        public static IEnumerable<TransitDb> ReadFrom(IEnumerable<string> paths)
        {
            var tdbs = new List<TransitDb>();
            var i = 0;
            foreach (var path in paths)
            {
                tdbs.Add(ReadFrom(path, (uint) i));
                i++;
            }

            return tdbs;
        }

        [Pure]
        public static TransitDb ReadFrom(string path, uint databaseId)
        {
            using (var stream = File.OpenRead(path))
            {
                return ReadFrom(stream, databaseId);
            }
        }

        /// <summary>
        /// Reads a transit db an all its data from the given stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="databaseId"></param>
        /// <returns>The transit db.</returns>
        [Pure]
        public static TransitDb ReadFrom(Stream stream, uint databaseId)
        {
            var version = stream.ReadByte();
            if (version != 1) throw new InvalidDataException($"Cannot read {nameof(TransitDb)}, invalid version #.");

            var stopsDb = StopsDb.ReadFrom(stream, databaseId);
            var tripsDb = TripsDb.ReadFrom(stream, databaseId);
            var connectionsDb = ConnectionsDb.ReadFrom(stream, databaseId);

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
                Id = stopsDb.DatabaseId;
            }

            public uint Id { get; }

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

                _stopsDb = parent._latestSnapshot.StopsDb.Clone();
                _tripsDb = parent._latestSnapshot.TripsDb.Clone();
                _connectionsDb = parent._latestSnapshot.ConnectionsDb.Clone();
            }

            /// <summary>
            /// Adds or updates a stop.
            /// </summary>
            /// <param name="globalId">The global id.</param>
            /// <param name="longitude">The longitude.</param>
            /// <param name="latitude">The latitude.</param>
            /// <param name="attributes">The attributes.</param>
            /// <returns>The stop id.</returns>
            public StopId AddOrUpdateStop(string globalId, double longitude, double latitude,
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
            public TripId AddOrUpdateTrip(string globalId, IEnumerable<Attribute> attributes = null)
            {
                if (_tripsDb.Get(globalId, out var found))
                {
                    return found.Id;
                }

                return _tripsDb.Add(globalId, attributes);
            }

            /// <summary>
            /// Adds or updates a connection.
            /// </summary>
            /// <param name="stop1">The first stop.</param>
            /// <param name="stop2">The second stop.</param>
            /// <param name="globalId">The global id.</param>
            /// <param name="departureTime">The departure time.</param>
            /// <param name="travelTime">The travel time in seconds.</param>
            /// <param name="departureDelay">The departure delay time in seconds.</param>
            /// <param name="arrivalDelay">The arrival delay time in seconds.</param>
            /// <param name="tripId">The trip id.</param>
            /// <param name="mode"></param>
            /// <returns></returns>
            public ConnectionId AddOrUpdateConnection(StopId stop1,
                StopId stop2, string globalId, DateTime departureTime, ushort travelTime,
                ushort departureDelay, ushort arrivalDelay, TripId tripId, ushort mode)
            {
                var internalId = _connectionsDb.AddOrUpdate(
                    new Connection(
                        new ConnectionId(0,0),
                        globalId,
                        stop1,
                        stop2,
                        departureTime.ToUnixTime(),
                        travelTime,
                        arrivalDelay,
                        departureDelay,
                        mode,
                        tripId
                        )
                    );
                return new ConnectionId(_connectionsDb.DatabaseId, internalId);
            }

            public uint AddOrUpdateConnection(Connection c)
            {
                return _connectionsDb.AddOrUpdate(c);
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