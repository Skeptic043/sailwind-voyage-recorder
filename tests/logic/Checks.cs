using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace VoyageRecorder
{
    internal static class Checks
    {
        private static int failures;

        private static void Check(bool condition, string name)
        {
            Console.WriteLine((condition ? "PASS: " : "FAIL: ") + name);
            if (!condition) failures++;
        }

        private static JsonElement Parse(string line)
        {
            Check(line.EndsWith("\n") && line.IndexOf('\n') == line.Length - 1, "line is one line ending in a newline: " + line.TrimEnd());
            return JsonDocument.Parse(line).RootElement.Clone();
        }

        private static int Main()
        {
            // A decimal-comma culture must never reach the file.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
            LogFormat.Now = () => new DateTime(2026, 11, 15, 12, 0, 0, 500, DateTimeKind.Utc);
            IslandContacts.Check(Check);
            Plugin.CheckPortVisits(Check);

            var header = Parse(LogFormat.Header("0.1.0", 2));
            Check(header.GetProperty("type").GetString() == "header", "header type");
            Check(header.GetProperty("format").GetString() == "voyage-recorder", "header format");
            Check(header.GetProperty("version").GetInt32() == 1, "header version");
            Check(header.GetProperty("mod").GetString() == "0.1.0", "header mod version");
            Check(header.GetProperty("slot").GetInt32() == 2, "header slot");

            string sessionLine = LogFormat.Session(12, 7.123456, null);
            var session = Parse(sessionLine);
            Check(session.GetProperty("type").GetString() == "session", "session type");
            Check(session.GetProperty("day").GetInt32() == 12 && session.GetProperty("hour").GetDouble() == 7.1235,
                "session day and hour, hour to 4 places");
            Check(sessionLine.Contains("\"hour\":7.1235}"), "session hour uses a decimal point");
            Check(header.GetProperty("utc").GetDouble() == 1794744000.5 && session.GetProperty("utc").GetDouble() == 1794744000.5,
                "every line has the real time in Unix seconds with milliseconds");
            Check(sessionLine.StartsWith("{\"type\":\"session\",\"utc\":"), "utc follows the type");
            Check(!session.TryGetProperty("world", out _) && !session.TryGetProperty("plugins", out _), "session without world or plugins leaves them out");

            var vanilla = Parse(LogFormat.Session(1, 6, "g", new WorldInfo(), new[] { new LoadedPlugin { Guid = "a.b", Name = "A \"B\"", Version = "1.2.3" } }));
            Check(vanilla.GetProperty("world").GetProperty("kind").GetString() == "vanilla" && vanilla.GetProperty("world").EnumerateObject().Count() == 1,
                "a vanilla world is just its kind");
            var plugin = vanilla.GetProperty("plugins")[0];
            Check(plugin.GetProperty("guid").GetString() == "a.b" && plugin.GetProperty("name").GetString() == "A \"B\"" && plugin.GetProperty("version").GetString() == "1.2.3",
                "plugins list each guid, name and version");
            var scrambled = Parse(LogFormat.Session(1, 6, "g", new WorldInfo
            {
                Scrambled = true, Seed = -12345, IslandSpread = 10000, MinArchipelagoSeparation = 30000, MinIslandSeparation = 2000,
                LonMin = -12, LonMax = 32, LatMin = 26, LatMax = 46, BorderExpander = 1, Scrambler = 60, ScrambledSeas = "6.0.2", Layout = "00ff"
            }, new LoadedPlugin[0])).GetProperty("world");
            Check(scrambled.GetProperty("kind").GetString() == "scrambled" && scrambled.GetProperty("seed").GetInt32() == -12345
                && scrambled.GetProperty("latMax").GetInt32() == 46 && scrambled.GetProperty("scrambler").GetInt32() == 60
                && scrambled.GetProperty("scrambledSeas").GetString() == "6.0.2" && scrambled.GetProperty("layout").GetString() == "00ff",
                "a Scrambled Seas world has its seed, settings, versions and layout");
            string layout = LogFormat.LayoutFingerprint(new[] { 1000.4, -2.0, 30000.0 });
            Check(layout.Length == 16 && layout == LogFormat.LayoutFingerprint(new[] { 999.6, -2.2, 30000.1 }), "layout fingerprint ignores sub-metre differences");
            Check(layout != LogFormat.LayoutFingerprint(new[] { -2.0, 1000.0, 30000.0 }), "layout fingerprint depends on order");
            Check(!session.TryGetProperty("game", out _), "session without a game id leaves it out");
            var gameSession = Parse(LogFormat.Session(12, 7.5, "0a1b2c3d4e5f"));
            Check(gameSession.GetProperty("game").GetString() == "0a1b2c3d4e5f", "session carries the game id");

            var full = new Reading
            {
                Day = 12, Hour = 7.25, Lat = 31.123456789, Lon = -1.5, Event = "sleep", Boat = "medi medium",
                Course = 214.34, Knots = 5.86, WindFrom = 71.0, WindKnots = 11.24,
            };
            string fullLine = LogFormat.Position(full);
            var position = Parse(fullLine);
            Check(fullLine.Contains("31.1234568") && fullLine.Contains("\"lon\":-1.5,"), "latitude and longitude use a decimal point, 7 places");
            Check(position.GetProperty("type").GetString() == "position", "position type");
            Check(position.GetProperty("event").GetString() == "sleep", "event recorded");
            Check(position.GetProperty("boat").GetString() == "medi medium", "boat recorded");
            Check(position.GetProperty("course").GetDouble() == 214.3 && position.GetProperty("knots").GetDouble() == 5.9,
                "course and speed to one place");
            Check(position.GetProperty("windFrom").GetDouble() == 71 && position.GetProperty("windKnots").GetDouble() == 11.2,
                "wind to one place");

            var bare = Parse(LogFormat.Position(new Reading { Day = 3, Hour = 0, Lat = -0.00000001, Lon = 0 }));
            Check(!bare.TryGetProperty("event", out _) && !bare.TryGetProperty("boat", out _) && !bare.TryGetProperty("course", out _)
                && !bare.TryGetProperty("knots", out _) && !bare.TryGetProperty("windFrom", out _) && !bare.TryGetProperty("windKnots", out _),
                "fields that are off or not applicable are left out");
            Check(LogFormat.Position(new Reading { Lat = -0.00000001 }).Contains("\"lat\":0,"), "a value that rounds to zero is written as 0, not -0");

            var quoted = Parse(LogFormat.Position(new Reading { Boat = "odd \"name\"\\\t" }));
            Check(quoted.GetProperty("boat").GetString() == "odd \"name\"\\\t", "boat names with quotes, backslashes and control characters stay valid JSON");

            var hull = Parse(LogFormat.Position(new Reading { Day = 1, Boat = "dhow medium", Hull = 37 }));
            Check(hull.GetProperty("hull").GetInt32() == 37, "hull damage recorded in percent");

            var port = Parse(LogFormat.Port(new PortVisit
            {
                Day = 4, Hour = 9.5, Port = "Gold Rock City", Island = 1, Lat = 30.1234567, Lon = -2.5, PortLat = 30.2, PortLon = -2.45,
                OffsetLat = 14.0347422, OffsetLon = 32.5261333
            }));
            Check(port.GetProperty("offsetLat").GetDouble() == 14.0347422 && port.GetProperty("offsetLon").GetDouble() == 32.5261333,
                "port line has the island's Scrambled Seas offset");
            Check(port.GetProperty("type").GetString() == "port" && port.GetProperty("port").GetString() == "Gold Rock City"
                && port.GetProperty("island").GetInt32() == 1, "port line names the port and its island");
            Check(port.GetProperty("lat").GetDouble() == 30.1234567 && port.GetProperty("portLon").GetDouble() == -2.45,
                "port line has the island centre and the port position");

            var keyed = Parse(LogFormat.Session(1, 6, "g", null, null, new RecorderKeys { Record = "J", Location = "V", Marker = "V + LeftShift" }));
            Check(keyed.GetProperty("keys").GetProperty("record").GetString() == "J" && keyed.GetProperty("keys").GetProperty("marker").GetString() == "V + LeftShift",
                "session line carries Voyage Recorder's keys");
            var keysLine = Parse(LogFormat.KeysChanged(new RecorderKeys { Record = "K", Location = "V", Marker = "V + LeftShift" }));
            Check(keysLine.GetProperty("type").GetString() == "keys" && keysLine.GetProperty("keys").GetProperty("record").GetString() == "K", "keys line when a key changes");

            var depart = Parse(LogFormat.Depart(new PortDeparture { Day = 4, Hour = 11.25, Port = "Gold Rock City", Island = 1 }));
            Check(depart.GetProperty("type").GetString() == "depart" && depart.GetProperty("port").GetString() == "Gold Rock City"
                && depart.GetProperty("island").GetInt32() == 1 && depart.GetProperty("hour").GetDouble() == 11.25, "depart line names the port and time");

            var landfall = Parse(LogFormat.Landfall(new LandfallVisit
            {
                Day = 4, Hour = 11.25, Source = "terra-incognita", IslandId = "terra incognita Atoll (route) #12",
                Name = "Atoll (route) #12", Lat = 30.201, Lon = -2.451, IslandLat = 30.205, IslandLon = -2.455
            }));
            Check(landfall.GetProperty("type").GetString() == "landfall" && landfall.GetProperty("source").GetString() == "terra-incognita"
                && landfall.GetProperty("islandId").GetString() == "terra incognita Atoll (route) #12"
                && landfall.GetProperty("name").GetString() == "Atoll (route) #12", "landfall identifies the specific generated island");
            Check(landfall.GetProperty("day").GetInt32() == 4 && landfall.GetProperty("hour").GetDouble() == 11.25
                && landfall.GetProperty("lat").GetDouble() == 30.201 && landfall.GetProperty("lon").GetDouble() == -2.451
                && landfall.GetProperty("islandLat").GetDouble() == 30.205 && landfall.GetProperty("islandLon").GetDouble() == -2.455,
                "landfall keeps the confirmation time, player position and island centre");
            Check(!landfall.TryGetProperty("offsetLat", out _) && !landfall.TryGetProperty("offsetLon", out _),
                "generated landfalls leave out the native island offset");
            var movedLandfall = Parse(LogFormat.Landfall(new LandfallVisit
            {
                Source = "native", IslandId = "38", OffsetLat = 2.8751234, OffsetLon = 7.7504321
            }));
            Check(movedLandfall.GetProperty("offsetLat").GetDouble() == 2.8751234 && movedLandfall.GetProperty("offsetLon").GetDouble() == 7.7504321,
                "native landfalls include the Scrambled Seas offset for the chart outline");
            CheckLandfallVisits();

            var note = Parse(LogFormat.Note(new Reading { Day = 5, Hour = 13.5, Lat = 31, Lon = -2, Boat = "dhow medium", Course = 90, Knots = 4 }, "abc123", "Whales here\nlots of them"));
            Check(note.GetProperty("type").GetString() == "note" && note.GetProperty("id").GetString() == "abc123"
                && note.GetProperty("text").GetString() == "Whales here\nlots of them" && note.GetProperty("boat").GetString() == "dhow medium"
                && note.GetProperty("course").GetDouble() == 90 && note.GetProperty("lat").GetDouble() == 31, "note line has id, text and the reading's details");
            Check(!note.TryGetProperty("event", out _), "a note isn't a mark");
            var marked = Parse(LogFormat.Position(new Reading { Day = 5, Hour = 13.5, Lat = 31, Lon = -2, Event = "race-start", Id = "m1" }));
            Check(marked.GetProperty("id").GetString() == "m1" && marked.GetProperty("event").GetString() == "race-start", "a mark carries its id");
            var undo = Parse(LogFormat.Undo("m1"));
            Check(undo.GetProperty("type").GetString() == "undo" && undo.GetProperty("id").GetString() == "m1", "an undo line names the mark it takes back");
            var longNote = Parse(LogFormat.Note(new Reading(), "x", new string('a', LogFormat.MaxNoteLength + 50)));
            Check(longNote.GetProperty("text").GetString().Length == LogFormat.MaxNoteLength, "long notes are cut to the limit");

            var heading = Parse(LogFormat.Mission(new MissionHint
            {
                Day = 4, Hour = 10, Origin = "Gold Rock City", OriginIsland = 1, OriginLat = 30.2, OriginLon = -2.45,
                Destination = "Fort Aestrin", DestinationIsland = 15, Heading = 22.5, Points = 16, Miles = 214
            }));
            Check(heading.GetProperty("type").GetString() == "mission" && heading.GetProperty("destination").GetString() == "Fort Aestrin"
                && heading.GetProperty("heading").GetDouble() == 22.5 && heading.GetProperty("points").GetInt32() == 16
                && heading.GetProperty("miles").GetInt32() == 214, "heading mission line");
            Check(!heading.TryGetProperty("approxLat", out _), "a heading mission has no coordinates");
            var coords = Parse(LogFormat.Mission(new MissionHint { Origin = "A", Destination = "B", ApproxLat = 31.5, ApproxLon = -3, Decimals = 1 }));
            Check(coords.GetProperty("approxLat").GetDouble() == 31.5 && coords.GetProperty("decimals").GetInt32() == 1
                && !coords.TryGetProperty("heading", out _) && !coords.TryGetProperty("miles", out _), "coordinates mission line without a shown distance");

            // Scrambled Seas rounds to the nearest of its directions, halves to even.
            Check(LogFormat.CompassHeading(10, 16) == 0 && LogFormat.CompassHeading(12, 16) == 22.5, "16-point heading rounds to the nearest direction");
            Check(LogFormat.CompassHeading(355, 16) == 0, "a heading just west of north wraps to north");
            Check(LogFormat.CompassHeading(33.75, 16) == 45 && LogFormat.CompassHeading(11.25, 16) == 0, "halfway headings round to even, as Math.Round does");
            Check(LogFormat.CompassHeading(100, 32) == 101.25, "32-point heading");

            Check(LogFormat.Bearing(0, 1) == 0, "north is 0");
            Check(LogFormat.Bearing(1, 0) == 90, "east is 90");
            Check(LogFormat.Bearing(0, -1) == 180, "south is 180");
            Check(LogFormat.Bearing(-1, 0) == 270, "west is 270");
            Check(LogFormat.Bearing(-0.0001, 1) == 0, "a bearing just west of north rounds to 0, not 360");
            Check(LogFormat.Bearing(1, 1) == 45, "northeast is 45");

            Check(LogFormat.BoatName("BOAT medi medium (50)") == "medi medium", "boat number and BOAT prefix dropped");
            Check(LogFormat.BoatName("BOAT junk small singleroof(90)") == "junk small singleroof", "number without a space dropped");
            Check(LogFormat.BoatName("BOAT Shroud Large") == "Shroud Large", "name without a number kept");
            Check(LogFormat.BoatName("BOAT LEOPARD (207)(Clone)") == "LEOPARD", "clone suffix dropped");
            Check(LogFormat.BoatName("Raft (big)") == "Raft (big)", "brackets that are not a number kept");
            Check(LogFormat.BoatName(null) == "", "missing name is empty");

            Console.WriteLine(failures == 0 ? "All logic checks passed." : failures + " logic check(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void CheckLandfallVisits()
        {
            var visits = new IslandLandfalls();
            Check(!visits.ObserveIsland(10) && !visits.ObserveIsland(10), "starting ashore establishes a baseline");

            visits.ObserveOffIsland();
            Check(!visits.ObserveIsland(10), "a return from water or a boat needs confirmation");
            visits.ClearPending();
            Check(!visits.ObserveIsland(10) && visits.ObserveIsland(10), "an uncertain sample breaks confirmation");
            Check(visits.ObserveIsland(10), "a landing stays eligible until the write succeeds");
            visits.Recorded(10);
            visits.ClearPending();
            Check(!visits.ObserveIsland(10), "a jump or uncertain sample does not rearm a recorded visit");

            Check(!visits.ObserveIsland(20) && !visits.ObserveIsland(30) && visits.ObserveIsland(30),
                "a different island can be visited directly and needs two matching samples");
            visits.Reset();
            Check(!visits.ObserveIsland(30) && !visits.ObserveIsland(30), "a reset discards a pending landing and establishes a new baseline");

            Check(visits.ObserveIsland(30, manual: true), "the location key can record the baseline island immediately");
            visits.Recorded(30);
            visits.Rebaseline();
            Check(!visits.ObserveIsland(30, manual: true) && !visits.ObserveIsland(30),
                "manual and timed checks share deduplication across a recording toggle");
            visits.Rebaseline();
            Check(!visits.ObserveIsland(40) && visits.ObserveIsland(40, manual: true),
                "starting on a different island after a gap establishes a fresh baseline");
            visits.Recorded(40);
            Check(!visits.ObserveIsland(50) && visits.ObserveIsland(60, manual: true) && !visits.ObserveIsland(50),
                "an intervening manual check breaks confirmation of a different island even if the write fails");
        }
    }
}
