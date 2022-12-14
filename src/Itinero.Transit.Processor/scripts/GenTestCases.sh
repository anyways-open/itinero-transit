#! /bin/bash

cd ..
DATE="$1"
DURATION="12hours"
if [ -z "$2" ]
then
    DURATION="$2"
fi

dotnet run --rosm https://www.openstreetmap.org/relation/9413958 "$DATE"T06:00:00 86400 --write-transit-db fixed-test-cases-osm-CentrumbusBrugge-"$DATE".transitdb

dotnet run --rlc https://graph.irail.be/sncb/connections https://graph.irail.be/sncb/stops "$DATE"T07:00 "$DURATION" --write-transit-db fixed-test-cases-sncb-"$DATE".transitdb


echo "Loading all delijn datasets"
dotnet run --rlc "https://openplanner.ilabt.imec.be/delijn/Antwerpen/connections"     "https://openplanner.ilabt.imec.be/delijn/Antwerpen/stops" "$DATE"T06:00:00 "$DURATION" --write-transit-db fixed-test-cases-de-lijn-ant-"$DATE".transitdb

dotnet run --rlc "https://openplanner.ilabt.imec.be/delijn/Limburg/connections"         "https://openplanner.ilabt.imec.be/delijn/Limburg/stops" "$DATE"T06:00:00 "$DURATION" --write-transit-db fixed-test-cases-de-lijn-lim-"$DATE".transitdb

dotnet run --rlc "https://openplanner.ilabt.imec.be/delijn/Oost-Vlaanderen/connections" "https://openplanner.ilabt.imec.be/delijn/Oost-Vlaanderen/stops" "$DATE"T06:00:00 "$DURATION" --write-transit-db fixed-test-cases-de-lijn-ovl-"$DATE".transitdb

dotnet run --rlc "https://openplanner.ilabt.imec.be/delijn/Vlaams-Brabant/connections"  "https://openplanner.ilabt.imec.be/delijn/Vlaams-Brabant/stops" "$DATE"T06:00:00 "$DURATION" --write-transit-db fixed-test-cases-de-lijn-vlb-"$DATE".transitdb

dotnet run --rlc "https://openplanner.ilabt.imec.be/delijn/West-Vlaanderen/connections" "https://openplanner.ilabt.imec.be/delijn/West-Vlaanderen/stops" "$DATE"T06:00:00 "$DURATION" --write-transit-db fixed-test-cases-de-lijn-wvl-"$DATE".transitdb

# mv *.transitdb ../../test/Itinero.Transit.Tests.Functional/testdata/
    
