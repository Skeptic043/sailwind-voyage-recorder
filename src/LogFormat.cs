using System;
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

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Header(string modVersion, int slot) =>
            new Line("header").Text("format", Format).Int("version", Version).Text("mod", modVersion).Int("slot", slot).End();

        public static string Session(int day, double hour, string game) =>
            new Line("session").Int("day", day).Number("hour", hour, 4).Text("game", game).End();

        public static string Position(Reading reading) =>
            new Line("position").Int("day", reading.Day).Number("hour", reading.Hour, 4)
                .Number("lat", reading.Lat, 7).Number("lon", reading.Lon, 7)
                .Text("event", reading.Event).Text("boat", reading.Boat)
                .Number("course", reading.Course, 1).Number("knots", reading.Knots, 1)
                .Number("windFrom", reading.WindFrom, 1).Number("windKnots", reading.WindKnots, 1)
                .Int("hull", reading.Hull).End();

        public static string Port(PortVisit visit) =>
            new Line("port").Int("day", visit.Day).Number("hour", visit.Hour, 4).Text("port", visit.Port).Int("island", visit.Island)
                .Number("lat", visit.Lat, 7).Number("lon", visit.Lon, 7).Number("portLat", visit.PortLat, 7).Number("portLon", visit.PortLon, 7)
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

        /// <summary>One JSON line. Null values are left out.</summary>
        private sealed class Line
        {
            private readonly StringBuilder text = new StringBuilder(200);

            public Line(string type) => text.Append("{\"type\":").Append(Quote(type));

            public Line Int(string name, int? value)
            {
                if (value.HasValue) text.Append(",\"").Append(name).Append("\":").Append(value.Value.ToString(Invariant));
                return this;
            }

            public Line Number(string name, double? value, int decimals)
            {
                if (!value.HasValue) return this;
                double rounded = Math.Round(value.Value, decimals);
                // Never write "-0".
                if (rounded == 0) rounded = 0;
                text.Append(",\"").Append(name).Append("\":").Append(rounded.ToString("0." + new string('#', decimals), Invariant));
                return this;
            }

            public Line Text(string name, string value)
            {
                if (!string.IsNullOrEmpty(value)) text.Append(",\"").Append(name).Append("\":").Append(Quote(value));
                return this;
            }

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
