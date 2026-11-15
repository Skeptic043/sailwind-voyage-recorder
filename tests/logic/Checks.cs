using System;
using System.Globalization;
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
    }
}
