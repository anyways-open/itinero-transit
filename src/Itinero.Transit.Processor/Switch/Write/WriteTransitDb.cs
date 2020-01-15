﻿// The MIT License (MIT)

// Copyright (c) 2016 Ben Abelshausen

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using Itinero.Transit.Data;
using Itinero.Transit.Utils;

namespace Itinero.Transit.Processor.Switch.Write
{
    /// <summary>
    /// Represents a switch to read a shapefile for routing.
    /// </summary>
    class WriteTransitDb : DocumentedSwitch, ITransitDbSink
    {
        private static readonly string[] _names =
            {"--write-transit-db", "--write-transitdb", "--write-transit", "--write", "--wt"};

        private static readonly string _about = "Write a transitDB to disk";


        private static readonly List<(List<string> args, bool isObligated, string comment, string defaultValue)>
            _extraParams =
                new List<(List<string> args, bool isObligated, string comment, string defaultValue)>
                {
                    SwitchesExtensions.obl("file", "The output file to write to"),
                };

        private const bool IsStable = true;


        public WriteTransitDb()
            : base(_names, _about, _extraParams, IsStable)
        {
        }


        public void Use(Dictionary<string, string> arguments, TransitDbSnapShot tdb)
        {
            var fileName = arguments["file"];

            using (var stream = File.OpenWrite(fileName))
            {
                tdb.WriteTo(stream);
                Console.WriteLine($"Written {fileName}, transitDb is valid from {tdb.ConnectionsDb.EarliestDate.FromUnixTime():s} till {tdb.ConnectionsDb.LatestDate.FromUnixTime():s} ");
            }
        }
    }
}