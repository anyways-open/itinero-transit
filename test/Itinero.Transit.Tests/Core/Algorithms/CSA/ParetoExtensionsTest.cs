using Itinero.Transit.Algorithms.CSA;
using Itinero.Transit.Data;
using Itinero.Transit.Journey;
using Itinero.Transit.Journey.Metric;
using Itinero.Transit.OtherMode;
using Xunit;

namespace Itinero.Transit.Tests.Core.Algorithms.CSA
{
    public class ParetoExtensionsTest
    {
        [Fact]
        public void ExtendFrontiersTest()
        {
            // We have to use backwards journeys here as we have to use the ProfiledTransferCompare
            var loc0 = new StopId(0, 0, 0);
            var loc1 = new StopId(0, 0, 1);
            var loc2 = new StopId(0, 0, 2);
            var loc3 = new StopId(0, 0, 3);

            // Arrival at time 100
            var genesis = new Journey<TransferMetric>(loc2, 100, TransferMetric.Factory);

            // Departs at Loc1 at 90
            var atLoc1 = genesis.ChainBackward(
                new Connection(new ConnectionId(0,0), "0", loc1, loc2, 90, 10, 0, 0, 0, new TripId(0, 0)));

            // Departs at Loc0 at 50, no transfers
            var direct = atLoc1.ChainBackward(
                new Connection(new ConnectionId(0,1), "1", loc0, loc1, 50, 10, 0, 0, 0, new TripId(0, 0)));
            // Departs at Loc0 at 60, one transfers
            var transfered = atLoc1.ChainBackward(
                new Connection(new ConnectionId(0,2), "2", loc0, loc1, 60, 10, 0, 0, 0, new TripId(0, 1)));

            var loc0Frontier = new ProfiledParetoFrontier<TransferMetric>(TransferMetric.ParetoCompare, null);
            loc0Frontier.AddToFrontier(transfered);
            loc0Frontier.AddToFrontier(direct);
            
            var extended = loc0Frontier.ExtendFrontierBackwards(
                // Arrives at loc0 at 55 => direct can not be taken anymore, transfered can
                new DummyReader(), new Connection(new ConnectionId(0,6), "6", loc3, loc0, 45, 10, 0, 0, 0, new TripId(0, 1)),
                new InternalTransferGenerator());
            
            Assert.Equal(transfered, extended.Frontier[0].PreviousLink);
            Assert.Single(extended.Frontier);
            
            extended = loc0Frontier.ExtendFrontierBackwards(
                // Arrives at loc0 at 45 => both direct and transfered can be taken
                new DummyReader(), 
                new Connection(new ConnectionId(0,6), "6", loc3, loc0, 35, 10, 0, 0, 0,
                    new TripId(0, 1)),
                new InternalTransferGenerator(0));
            // We expect both routes to be in the frontier... They are, but in a merged way
            // ------------------------------------------- last connection - transferedJourney
            Assert.Equal(transfered, extended.Frontier[0]
                .PreviousLink // A fake element - this is the merging journey
                .PreviousLink // The actual journey
            );
            // -------------------------------------- lest connection -------- transferObject - directjourney
            Assert.Equal(direct, extended.Frontier[0].AlternativePreviousLink.PreviousLink.PreviousLink);
        }
        

        [Fact]
        public void CombineFrontiersTest()
        {
            var loc0 = new StopId(0, 0, 0);
            var loc1 = new StopId(0, 0, 1);
            var loc2 = new StopId(0, 0, 2);


            // Genesis at time 60
            var genesis = new Journey<TransferMetric>(loc0, 60, TransferMetric.Factory);

            // Departs from Loc1 at time 10s needed
            var atLoc1 = genesis.ChainBackward(
                new Connection(new ConnectionId(0,0), "0", loc0, loc1, 50, 10, 0, 0, 0, new TripId(0, 0)));

            // Departs from Loc2 (destination) at 25s needed, without transfer but slightly slow 
            var direct = atLoc1.ChainBackward(
                new Connection(new ConnectionId(0,1), "1", loc1, loc2, 35, 10, 0, 0, 0, new TripId(0, 0)));

            // Departs from Loc2 (destination) slightly faster (at 21s needed) but with one transfer
            var transferedFast = atLoc1.ChainBackward(
                new Connection(new ConnectionId(0,2), "2", loc1, loc2, 39, 10, 0, 0, 0, new TripId(0, 1)));

            // Departs from Loc2 (destination) slightly slower (at 23s needed) and with one transfer - suboptimal
            var transferedSlow = atLoc1.ChainBackward(
                new Connection(new ConnectionId(0,3), "3", loc1, loc2, 37, 10, 0, 0, 0, new TripId(0, 1)));


            // And now we add those to pareto frontier to test their behaviour
            var frontier0 = new ProfiledParetoFrontier<TransferMetric>(TransferMetric.ParetoCompare, null);

            Assert.True(frontier0.AddToFrontier(direct));
            Assert.True(frontier0.AddToFrontier(transferedFast));


            var frontier1 = new ProfiledParetoFrontier<TransferMetric>(TransferMetric.ParetoCompare, null);

            Assert.True(frontier1.AddToFrontier(direct));
            Assert.True(frontier1.AddToFrontier(transferedSlow));

            var frontier = ParetoExtensions.Combine(frontier0, frontier1);

            Assert.Equal(2, frontier.Frontier.Count);
            Assert.Equal(direct, frontier.Frontier[0]);
            Assert.Equal(transferedFast, frontier.Frontier[1]);
            Assert.DoesNotContain(transferedSlow, frontier.Frontier);
        }
    }
}