using System;
using System.Threading;
using Itinero.Transit.Data;
using Itinero.Transit.Data.Synchronization;
using Itinero.Transit.IO.LC;
using Itinero.Transit.Tests.Functional.Utils;

namespace Itinero.Transit.Tests.Functional.IO.LC.Synchronization
{
    public class TestAutoUpdating : FunctionalTest
    {
        
        public override string Name => "Auto updating test";

        protected override void Execute()
        {
            var tdb = new TransitDb(0);

            var sncb = Belgium.AllLinks["sncb"];
            var (syncer, _) = tdb.UseLinkedConnections(
                sncb.connections,
                sncb.locations,
                new SynchronizedWindow(5, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(14400)));

            syncer.Start();
            // There is a default initial delay of 1 second
            Thread.Sleep(1500);
            NotNull(syncer.CurrentlyRunning);
            NotNull(syncer.CurrentlyRunning.ToString());
            
            Thread.Sleep(1000);
            syncer.Stop();
        }
    }
}