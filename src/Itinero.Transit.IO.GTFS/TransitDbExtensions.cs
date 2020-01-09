using System;
using System.IO;
using Itinero.Transit.Data;
using Itinero.Transit.IO.GTFS.Data;

namespace Itinero.Transit.IO.GTFS
{
    public static class TransitDbExtensions
    {
        public static void UseGtfs(this TransitDb tdb, string archivepath, DateTime startdate, DateTime enddate)
        {
            var gtfs = new Gtfs2Tdb(archivepath);
            var wr = tdb.GetWriter();
            gtfs.AddDataBetween(wr, startdate, enddate);
            wr.Close();
        }
    }
}