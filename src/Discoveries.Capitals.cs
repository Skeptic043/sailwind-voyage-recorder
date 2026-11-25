using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoyageRecorder
{
    internal static partial class Discoveries
    {
        // Each capital's island, and the islands without a port in its region with the names Chartroom gives them,
        // with the secret ones, which Saffron joins although it has a port. Bone Isle is in Scrambled Seas' default
        // Aestrin region. Fire Fish Town's region has no landable one.
        private static readonly Dictionary<int, (int Island, string Name, bool Secret)[]> PortlessByCapital = new Dictionary<int, (int, string, bool)[]>
        {
            [1] = new[] { (5, "Isle of Clear Mind", false), (6, "Lion's Fang", false), (32, "Eleann Island", true), (42, "Saffron", true) },
            [15] = new[] { (23, "Oracle", false), (36, "Bone Isle", true) },
            [9] = new[] { (39, "Pondering Peak", false) }
        };

        /// <summary>
        /// From a capital's port, a hint toward each island without a port in its region, given the way Scrambled
        /// Seas gives mission hints: rounded coordinates, or a heading on its compass. Without a Scrambled Seas
        /// heading or coordinates, a heading on the 32-point compass. Always with the distance in miles, since a
        /// heading alone marks no area. Empty for any other port.
        /// </summary>
        public static List<MissionHint> CapitalHints(Port port, int day, double hour, bool secrets)
        {
            var hints = new List<MissionHint>();
            int capital = IslandOf(port);
            if (!PortlessByCapital.TryGetValue(capital, out var islands)) return hints;
            Vector3 originGlobe = FloatingOriginManager.instance.GetGlobeCoords(port.transform);
            string kind = ScrambledSeas.Active ? ScrambledSeas.HintKind : "";
            foreach (var (number, name, secret) in islands)
            {
                if (secret && !secrets) continue;
                Transform island = RegisteredIsland(number);
                if (island == null) continue;
                Vector3 islandGlobe = FloatingOriginManager.instance.GetGlobeCoords(island);
                var hint = new MissionHint
                {
                    Day = day, Hour = hour, Source = "capital",
                    Origin = port.GetPortName(), OriginIsland = capital, OriginLat = originGlobe.z, OriginLon = originGlobe.x,
                    Destination = name, DestinationIsland = number,
                    Miles = (int)Math.Round(Vector3.Distance(port.transform.position, island.position) * Sun.sun.initialTimescale / 0.514444)
                };
                if (kind == "Coords")
                {
                    int decimals = ScrambledSeas.Decimals;
                    hint.ApproxLat = Math.Round(islandGlobe.z, decimals);
                    hint.ApproxLon = Math.Round(islandGlobe.x, decimals);
                    hint.Decimals = decimals;
                }
                else
                {
                    int points = kind == "Heading" ? ScrambledSeas.Points : 32;
                    double degrees = kind == "Heading"
                        ? ScrambledSeas.HeadingDegrees(port.transform.position, island.position)
                        : LogFormat.Bearing(islandGlobe.x - originGlobe.x, islandGlobe.z - originGlobe.z);
                    hint.Heading = LogFormat.CompassHeading(degrees, points);
                    hint.Points = points;
                }
                hints.Add(hint);
            }
            return hints;
        }
    }
}
