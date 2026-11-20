using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VoyageRecorder
{
    /// <summary>One recorded moment. Optional values are null when switched off or not applicable.</summary>
    internal struct Reading
    {
        public int Day;
        public double Hour;
        public double Lat;
        public double Lon;
        public string Event;
        // Recorded locations and race markers carry an id, so an undo can name the one it takes back.
        public string Id;
        public string Boat;
        public double? Course;
        public double? Knots;
        public double? WindFrom;
        public double? WindKnots;
        public int? Hull;
    }

    /// <summary>A port reached: its island's centre, so a chart places the whole island, and the port itself.</summary>
    internal struct PortVisit
    {
        public int Day;
        public double Hour;
        public string Port;
        public int Island;
        public double Lat;
        public double Lon;
        public double PortLat;
        public double PortLon;
        public double? OffsetLat;
        public double? OffsetLon;
    }

    /// <summary>A port left: written when the player moves out of reach of the port they arrived at.</summary>
    internal struct PortDeparture
    {
        public int Day;
        public double Hour;
        public string Port;
        public int Island;
    }

    /// <summary>An island visited, with the player's position and the island root's position.</summary>
    internal struct LandfallVisit
    {
        public int Day;
        public double Hour;
        public string Source;
        public string IslandId;
        public string Name;
        public double Lat;
        public double Lon;
        public double IslandLat;
        public double IslandLon;
        public double? OffsetLat;
        public double? OffsetLon;
        public string Prefab;
        public int? Yaw;
    }

    /// <summary>
    /// The world a session is played in: vanilla, or a Scrambled Seas world described by the settings in its save
    /// and a fingerprint of the island layout those settings produced.
    /// </summary>
    internal sealed class WorldInfo
    {
        public bool Scrambled;
        public int Seed;
        public int IslandSpread;
        public int MinArchipelagoSeparation;
        public int MinIslandSeparation;
        public int LonMin;
        public int LonMax;
        public int LatMin;
        public int LatMax;
        public int BorderExpander;
        public int Scrambler;
        public string ScrambledSeas;
        public string Layout;
    }

    /// <summary>Voyage Recorder's own keys, as BepInEx writes them in its config ("J", "LeftControl + J").</summary>
    internal struct RecorderKeys
    {
        public string Record;
        public string Location;
        public string Marker;
    }

    /// <summary>A loaded BepInEx plugin, by its id, name and version.</summary>
    internal struct LoadedPlugin
    {
        public string Guid;
        public string Name;
        public string Version;
    }

    /// <summary>
    /// A mission as the mission screen describes it: where it starts, where it goes, and the hint
    /// Scrambled Seas gives, either a heading on its compass or rounded coordinates, with the distance when shown.
    /// </summary>
    internal struct MissionHint
    {
        public int Day;
        public double Hour;
        public string Origin;
        public int OriginIsland;
        public double OriginLat;
        public double OriginLon;
        public string Destination;
        public int DestinationIsland;
        public double? Heading;
        public int? Points;
        public double? ApproxLat;
        public double? ApproxLon;
        public int? Decimals;
        public int? Miles;
    }

    /// <summary>
    /// Writes Voyage Recorder log lines: one JSON object per line, always with invariant numbers,
    /// so a player's decimal comma never reaches the file. See FORMAT.md.
    /// </summary>
    internal static class LogFormat
    {
        public const string Format = "voyage-recorder";
        public const int Version = 1;

        // Game speeds are metres per second; the game's own speedometer shows knots as m/s × 1.944.
        public const double KnotsPerMetrePerSecond = 1.944;

        // Notes longer than this are cut; Chartroom keeps up to the same length.
        public const int MaxNoteLength = 20000;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Real time for each line's <c>utc</c>. Tests replace it.</summary>
        public static Func<DateTime> Now = () => DateTime.UtcNow;

        public static string Header(string modVersion, int slot) =>
            new Line("header").Text("format", Format).Int("version", Version).Text("mod", modVersion).Int("slot", slot).End();

        public static string Session(int day, double hour, string game, WorldInfo world = null, IList<LoadedPlugin> plugins = null, RecorderKeys? keys = null)
        {
            var line = new Line("session").Int("day", day).Number("hour", hour, 4).Text("game", game);
            if (keys.HasValue) line.Object("keys", Keys(keys.Value));
            if (world != null) line.Object("world", World(world));
            if (plugins != null)
            {
                var entries = new List<Line>(plugins.Count);
                foreach (LoadedPlugin plugin in plugins)
                    entries.Add(new Line().Text("guid", plugin.Guid).Text("name", plugin.Name).Text("version", plugin.Version));
                line.Array("plugins", entries);
            }
            return line.End();
        }

        /// <summary>Written when the player changes one of Voyage Recorder's keys during a session.</summary>
        public static string KeysChanged(RecorderKeys keys) => new Line("keys").Object("keys", Keys(keys)).End();

        private static Line Keys(RecorderKeys keys) =>
            new Line().Text("record", keys.Record).Text("location", keys.Location).Text("marker", keys.Marker);

        private static Line World(WorldInfo world)
        {
            if (!world.Scrambled) return new Line().Text("kind", "vanilla");
            return new Line().Text("kind", "scrambled").Int("seed", world.Seed)
                .Int("islandSpread", world.IslandSpread).Int("minArchipelagoSeparation", world.MinArchipelagoSeparation)
                .Int("minIslandSeparation", world.MinIslandSeparation)
                .Int("lonMin", world.LonMin).Int("lonMax", world.LonMax).Int("latMin", world.LatMin).Int("latMax", world.LatMax)
                .Int("borderExpander", world.BorderExpander).Int("scrambler", world.Scrambler)
                .Text("scrambledSeas", world.ScrambledSeas).Text("layout", world.Layout);
        }

        public static string Position(Reading reading) => ReadingFields(new Line("position"), reading).End();

        /// <summary>A note the player wrote, with the reading taken when they opened the note box.</summary>
        public static string Note(Reading reading, string id, string text)
        {
            string kept = text ?? "";
            if (kept.Length > MaxNoteLength) kept = kept.Substring(0, MaxNoteLength);
            reading.Id = id;
            return ReadingFields(new Line("note"), reading).Text("text", kept).End();
        }

        /// <summary>Takes back the recorded location or race marker with this id.</summary>
        public static string Undo(string id) => new Line("undo").Text("id", id).End();

        private static Line ReadingFields(Line line, Reading reading) =>
            line.Text("id", reading.Id).Int("day", reading.Day).Number("hour", reading.Hour, 4)
                .Number("lat", reading.Lat, 7).Number("lon", reading.Lon, 7)
                .Text("event", reading.Event).Text("boat", reading.Boat)
                .Number("course", reading.Course, 1).Number("knots", reading.Knots, 1)
                .Number("windFrom", reading.WindFrom, 1).Number("windKnots", reading.WindKnots, 1)
                .Int("hull", reading.Hull);

        public static string Port(PortVisit visit) =>
            new Line("port").Int("day", visit.Day).Number("hour", visit.Hour, 4).Text("port", visit.Port).Int("island", visit.Island)
                .Number("lat", visit.Lat, 7).Number("lon", visit.Lon, 7).Number("portLat", visit.PortLat, 7).Number("portLon", visit.PortLon, 7)
                .Number("offsetLat", visit.OffsetLat, 7).Number("offsetLon", visit.OffsetLon, 7).End();

        public static string Depart(PortDeparture departure) =>
            new Line("depart").Int("day", departure.Day).Number("hour", departure.Hour, 4)
                .Text("port", departure.Port).Int("island", departure.Island).End();

        public static string Landfall(LandfallVisit visit) =>
            new Line("landfall").Int("day", visit.Day).Number("hour", visit.Hour, 4)
                .Text("source", visit.Source).Text("islandId", visit.IslandId).Text("name", visit.Name)
                .Number("lat", visit.Lat, 7).Number("lon", visit.Lon, 7)
                .Number("islandLat", visit.IslandLat, 7).Number("islandLon", visit.IslandLon, 7)
                .Text("prefab", visit.Prefab).Int("yaw", visit.Yaw)
                .Number("offsetLat", visit.OffsetLat, 7).Number("offsetLon", visit.OffsetLon, 7).End();

        public static string Mission(MissionHint mission) =>
            new Line("mission").Int("day", mission.Day).Number("hour", mission.Hour, 4)
                .Text("origin", mission.Origin).Int("originIsland", mission.OriginIsland)
                .Number("originLat", mission.OriginLat, 7).Number("originLon", mission.OriginLon, 7)
                .Text("destination", mission.Destination).Int("destinationIsland", mission.DestinationIsland)
                .Number("heading", mission.Heading, 4).Int("points", mission.Points)
                .Number("approxLat", mission.ApproxLat, 6).Number("approxLon", mission.ApproxLon, 6).Int("decimals", mission.Decimals)
                .Int("miles", mission.Miles).End();

        /// <summary>
        /// A short fingerprint of an island layout: FNV-1a over every offset rounded to whole metres, as 16 hex
        /// digits. The same layout always gives the same fingerprint, whatever settings or version produced it.
        /// </summary>
        public static string LayoutFingerprint(IEnumerable<double> offsetsMetres)
        {
            ulong hash = 14695981039346656037UL;
            foreach (double value in offsetsMetres)
            {
                long rounded = (long)Math.Round(value);
                for (int i = 0; i < 8; i++)
                {
                    hash ^= (byte)(rounded >> (i * 8));
                    hash *= 1099511628211UL;
                }
            }
            return hash.ToString("x16", Invariant);
        }

        /// <summary>
        /// The compass bearing, 0 to under 360 degrees, of a horizontal direction given by its east and
        /// north parts (the game's x and z axes).
        /// </summary>
        public static double Bearing(double east, double north)
        {
            double degrees = Math.Atan2(east, north) * 180.0 / Math.PI;
            degrees = Math.Round(degrees, 1);
            return degrees < 0 ? degrees + 360.0 : degrees >= 360.0 ? degrees - 360.0 : degrees;
        }

        /// <summary>
        /// The boat's type name from its scene object name: "BOAT medi medium (50)" becomes "medi medium".
        /// The number differs between boats of one type, so it is dropped.
        /// </summary>
        public static string BoatName(string objectName)
        {
            string name = (objectName ?? "").Trim();
            if (name.EndsWith("(Clone)", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 7).TrimEnd();
            if (name.EndsWith(")", StringComparison.Ordinal))
            {
                int open = name.LastIndexOf('(');
                if (open >= 0 && IsDigits(name, open + 1, name.Length - 1)) name = name.Substring(0, open).TrimEnd();
            }
            if (name.StartsWith("BOAT ", StringComparison.Ordinal)) name = name.Substring(5).TrimStart();
            return name;
        }

        /// <summary>
        /// A heading as a compass of <paramref name="points"/> directions gives it: the nearest direction,
        /// rounding as Scrambled Seas does, in degrees.
        /// </summary>
        public static double CompassHeading(double degrees, int points)
        {
            double step = 360.0 / points;
            return (int)Math.Round(degrees / step) % points * step;
        }

        private static bool IsDigits(string text, int start, int end)
        {
            if (end <= start) return false;
            for (int i = start; i < end; i++)
                if (text[i] < '0' || text[i] > '9') return false;
            return true;
        }

        /// <summary>One JSON line, or an object inside one. Null values are left out.</summary>
        private sealed class Line
        {
            private readonly StringBuilder text = new StringBuilder(200);
            private bool empty = true;

            // Every line starts with its type and the real time it was written.
            public Line(string type)
            {
                text.Append("{\"type\":").Append(Quote(type));
                empty = false;
                Number("utc", (Now() - Epoch).TotalSeconds, 3);
            }

            // An object nested in a line.
            public Line() => text.Append('{');

            private StringBuilder Name(string name)
            {
                if (!empty) text.Append(',');
                empty = false;
                return text.Append('"').Append(name).Append("\":");
            }

            public Line Int(string name, int? value)
            {
                if (value.HasValue) Name(name).Append(value.Value.ToString(Invariant));
                return this;
            }

            public Line Number(string name, double? value, int decimals)
            {
                if (!value.HasValue) return this;
                double rounded = Math.Round(value.Value, decimals);
                // Never write "-0".
                if (rounded == 0) rounded = 0;
                Name(name).Append(rounded.ToString("0." + new string('#', decimals), Invariant));
                return this;
            }

            public Line Text(string name, string value)
            {
                if (!string.IsNullOrEmpty(value)) Name(name).Append(Quote(value));
                return this;
            }

            public Line Object(string name, Line value)
            {
                Name(name).Append(value.Close());
                return this;
            }

            public Line Array(string name, IEnumerable<Line> values)
            {
                StringBuilder builder = Name(name).Append('[');
                bool first = true;
                foreach (Line value in values)
                {
                    if (!first) builder.Append(',');
                    first = false;
                    builder.Append(value.Close());
                }
                builder.Append(']');
                return this;
            }

            private string Close() => text.Append('}').ToString();

            public string End() => text.Append("}\n").ToString();
        }

        private static string Quote(string text)
        {
            var quoted = new StringBuilder(text.Length + 2).Append('"');
            foreach (char c in text)
            {
                if (c == '"' || c == '\\') quoted.Append('\\').Append(c);
                else if (c < ' ') quoted.Append("\\u").Append(((int)c).ToString("x4", Invariant));
                else quoted.Append(c);
            }
            return quoted.Append('"').ToString();
        }
    }
}
