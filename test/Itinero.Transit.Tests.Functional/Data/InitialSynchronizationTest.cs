using System;
using System.Collections.Generic;
using System.Threading;
using Itinero.Transit.Data;
using Itinero.Transit.Data.Synchronization;
using Itinero.Transit.IO.LC;
using Itinero.Transit.Tests.Functional.Utils;

namespace Itinero.Transit.Tests.Functional.Data
{
    public class InitialSynchronizationTest : FunctionalTest
    {
        public override string Name => "Initial synchronization test";

        protected override void Execute()
        {
            var tdb = new TransitDb(0);
            var connections = "https://graph.irail.be/sncb/connections";
            var location = "https://graph.irail.be/sncb/stops";

            var syncPolicies = new List<ISynchronizationPolicy>
            {
                new SynchronizedWindow(10, TimeSpan.FromSeconds(0), TimeSpan.FromMinutes(10),
                    1)
            };

            var (sync, _) = tdb.UseLinkedConnections(connections, location, syncPolicies);

            sync.InitialRun();

            True(sync.LoadedTimeWindows.Count > 0);

            sync.Start();
            var timeout = 60 * 1000;
            while (sync.CurrentlyRunning == null)
            {
                // ... we wait till the task is running...
                Thread.Sleep(1);
                timeout--;
                if (timeout <= 0)
                {
                    throw new Exception("We should have seen the task running by now...");
                }
            }
        }
    }
}