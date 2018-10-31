using System;
using System.Collections.Generic;
using JsonLD.Core;

namespace Itinero.Transit.Belgium
{
    public static class DeLijn
    {
        private static readonly List<Uri> ProvincesLocations = new List<Uri>
        {
            new Uri("https://belgium.linkedconnections.org/delijn/Antwerpen/stops"),
            new Uri("https://belgium.linkedconnections.org/delijn/Oost-Vlaanderen/stops"),
            new Uri("https://belgium.linkedconnections.org/delijn/West-Vlaanderen/stops"),
            new Uri("https://belgium.linkedconnections.org/delijn/Vlaams-Brabant/stops"),
            new Uri("https://belgium.linkedconnections.org/delijn/Limburg/stops")
        };


        public static Profile<TransferStats> Profile(string storagePath, string routerdbPath)
        {
            storagePath = storagePath + "/deLijn";
            var loc = LocationProvider(new LocalStorage(storagePath));
            var connections = new LocallyCachedConnectionsProvider
            (new LinkedConnectionProvider(
                    new Uri("https://belgium.linkedconnections.org/delijn/West-Vlaanderen"),
                    "https://belgium.linkedconnections.org/delijn/West-Vlaanderen/connections{?departureTime}"),
                new LocalStorage(storagePath + "/timeTables"));
            var footpath = new TransferGenerator(routerdbPath);

            return new Profile<TransferStats>(connections, loc, footpath,
                TransferStats.Factory, TransferStats.ProfileCompare, TransferStats.ParetoCompare);
        }


        public static ILocationProvider LocationProvider(LocalStorage storage)
        {
            // ReSharper disable once RedundantArgumentDefaultValue
            var loader = new Downloader(false);
            var locations = new List<ILocationProvider>();
            foreach (var prov in ProvincesLocations)
            {
                var proc = new JsonLdProcessor(loader, prov);
                var lf = new CachedLocationsFragment(prov, proc, storage);
                locations.Add(lf);
            }

            locations.Add(OsmLocationMapping.Singleton);
            return new LocationCombiner(locations);
        }
    }
}