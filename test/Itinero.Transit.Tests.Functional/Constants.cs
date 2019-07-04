using System;

namespace Itinero.Transit.Tests.Functional
{
    public class Constants
    {
        
        public static DateTime TestDate = new DateTime(2019, 05, 30, 09, 00, 00).ToUniversalTime().Date;

        
        public const string ZandStraat = "https://data.delijn.be/stops/500562";
        public const string AzSintJan = "https://data.delijn.be/stops/502083";
        public const string Moereind = "https://data.delijn.be/stops/107455";


        public const string NearStationBruggeLatLon = "https://www.openstreetmap.org/#map=19/51.19764/3.21847";

        public const string StationBruggeOsm = "https://www.openstreetmap.org/node/6348496391";
        public const string CoiseauKaaiOsm = "https://www.openstreetmap.org/node/6348562147";
        public const string Howest = "https://data.delijn.be/stops/502132";
        public const string Antwerpen = "http://irail.be/stations/NMBS/008821006"; // Antwerpen centraal

        public const string Gent = "http://irail.be/stations/NMBS/008892007";
        
        public const string OsmCentrumShuttle = "testdata/fixed-test-cases-osm-CentrumbusBrugge2019-05-30.transitdb";
        public const string Nmbs = "testdata/fixed-test-cases-sncb-2019-05-30.transitdb";
        public const string DelijnWvl = "testdata/fixed-test-cases-de-lijn-wvl-2019-05-30.transitdb";
        public const string DelijnOVl = "testdata/fixed-test-cases-de-lijn-ovl-2019-05-30.transitdb";
        public const string DelijnVlB = "testdata/fixed-test-cases-de-lijn-vlb-2019-05-30.transitdb";
        public const string DelijnLim = "testdata/fixed-test-cases-de-lijn-lim-2019-05-30.transitdb";
        public const string DelijnAnt = "testdata/fixed-test-cases-de-lijn-ant-2019-05-30.transitdb";



    }
}