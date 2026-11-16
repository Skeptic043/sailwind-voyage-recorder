using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace VoyageRecorder
{
    /// <summary>
    /// Ports reached and missions described, read from the game and, for mission hints, from Scrambled Seas.
    /// Everything here runs only when a port is reached or a mission list shows.
    /// </summary>
    internal static class Discoveries
    {
        /// <summary>The game's island number ("island N") nearest the port, or -1.</summary>
        public static int IslandOf(Port port)
        {
            Transform[] islands = Refs.islands;
            if (islands == null || port == null) return -1;
            Vector3 position = port.transform.position;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < islands.Length; i++)
            {
                if (islands[i] == null) continue;
                float distance = (islands[i].position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        public static PortVisit Visit(Port port, int day, double hour)
        {
            int island = IslandOf(port);
            Vector3 portGlobe = FloatingOriginManager.instance.GetGlobeCoords(port.transform);
            Vector3 islandGlobe = island >= 0 ? FloatingOriginManager.instance.GetGlobeCoords(Refs.islands[island]) : portGlobe;
            // How far the island was moved from where the game places it: Scrambled Seas' offset, or none without
            // it. A chart moves its own island outline by this, as it does for an imported Scrambled Seas world.
            Vector3? offset = ScrambledSeas.IslandOffset(island);
            return new PortVisit
            {
                Day = day, Hour = hour, Port = port.GetPortName(), Island = island,
                Lat = islandGlobe.z, Lon = islandGlobe.x, PortLat = portGlobe.z, PortLon = portGlobe.x,
                OffsetLat = offset?.z / 9000.0, OffsetLon = offset?.x / 9000.0
            };
        }

        /// <summary>
        /// The mission as Scrambled Seas' mission screen gives it, or null when it gives no heading or
        /// coordinates (not installed, off for this game, or set to give no hint).
        /// </summary>
        public static MissionHint? Hint(Mission mission, int day, double hour)
        {
            if (mission?.originPort == null || mission.destinationPort == null) return null;
            if (!ScrambledSeas.Active)
            {
                Plugin.LogOnce("Missions aren't recorded: Scrambled Seas isn't installed or is off for this game.");
                return null;
            }
            Port origin = mission.originPort;
            Port destination = mission.destinationPort;
            Vector3 originGlobe = FloatingOriginManager.instance.GetGlobeCoords(origin.transform);
            var hint = new MissionHint
            {
                Day = day, Hour = hour,
                Origin = origin.GetPortName(), OriginIsland = IslandOf(origin), OriginLat = originGlobe.z, OriginLon = originGlobe.x,
                Destination = destination.GetPortName(), DestinationIsland = IslandOf(destination)
            };
            string kind = ScrambledSeas.HintKind;
            if (kind == "Heading")
            {
                int points = ScrambledSeas.Points;
                hint.Heading = LogFormat.CompassHeading(ScrambledSeas.HeadingDegrees(origin.transform.position, destination.transform.position), points);
                hint.Points = points;
            }
            else if (kind == "Coords")
            {
                Vector3 destinationGlobe = FloatingOriginManager.instance.GetGlobeCoords(destination.transform);
                int decimals = ScrambledSeas.Decimals;
                hint.ApproxLat = Math.Round(destinationGlobe.z, decimals);
                hint.ApproxLon = Math.Round(destinationGlobe.x, decimals);
                hint.Decimals = decimals;
            }
            else
            {
                Plugin.LogOnce("Missions aren't recorded: Scrambled Seas' Destination Hint is " + (kind == "" ? "missing" : kind) + ", so the mission screen gives no heading or coordinates.");
                return null;
            }
            // The distance as the mission screen shows it, in miles.
            if (!ScrambledSeas.HidesDistance)
                hint.Miles = Mathf.RoundToInt(mission.distance * 100f / (0.514444f / Sun.sun.initialTimescale));
            return hint;
        }

        /// <summary>The world this game is played in, read once for each session line.</summary>
        public static WorldInfo World() => ScrambledSeas.World();

        /// <summary>Every loaded BepInEx plugin, read once for each session line.</summary>
        public static List<LoadedPlugin> Plugins()
        {
            var plugins = new List<LoadedPlugin>();
            foreach (var info in Chainloader.PluginInfos.Values)
            {
                if (info?.Metadata == null) continue;
                plugins.Add(new LoadedPlugin { Guid = info.Metadata.GUID, Name = info.Metadata.Name, Version = info.Metadata.Version?.ToString() });
            }
            plugins.Sort((a, b) => string.CompareOrdinal(a.Guid, b.Guid));
            return plugins;
        }

        /// <summary>Scrambled Seas' settings and heading rule, found once by its plugin id.</summary>
        private static class ScrambledSeas
        {
            private const string PluginId = "com.nandbrew.scrambledseas";
            private static bool resolved;
            private static bool present;
            private static ConfigFile config;
            private static MethodInfo headingDegrees;
            private static FieldInfo enabled;
            private static PropertyInfo saveContainer;
            private static string version;

            private static void Resolve()
            {
                if (resolved) return;
                resolved = true;
                if (!Chainloader.PluginInfos.TryGetValue(PluginId, out var info) || info.Instance == null) return;
                present = true;
                Assembly assembly = info.Instance.GetType().Assembly;
                headingDegrees = assembly.GetType("ScrambledSeas.CompassRose")?.GetMethod("GetHeadingDegrees", BindingFlags.Public | BindingFlags.Static);
                Type main = assembly.GetType("ScrambledSeas.Main");
                enabled = main?.GetField("pluginEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                saveContainer = main?.GetProperty("saveContainer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                config = info.Instance.Config;
                version = info.Metadata.Version?.ToString();
                if (headingDegrees == null || enabled == null)
                {
                    Plugin.LogOnce("Scrambled Seas was found, but not the parts mission hints need, so missions aren't recorded.");
                    config = null;
                }
            }

            // Scrambled Seas can be turned off for a game from the new game screen.
            public static bool Active
            {
                get
                {
                    Resolve();
                    return config != null && enabled.GetValue(null) is bool on && on;
                }
            }

            // Scrambled Seas moves Refs.islands[i + 1] by islandOffsets[i], in metres. An island it didn't move,
            // or a game it's off for, has no offset; null means the offset couldn't be read.
            public static Vector3? IslandOffset(int island)
            {
                if (!Active) return Vector3.zero;
                if (saveContainer == null || island < 1) return null;
                object container = saveContainer.GetValue(null, null);
                if (!(container?.GetType().GetProperty("islandOffsets")?.GetValue(container, null) is Vector3[] offsets)) return null;
                return island - 1 < offsets.Length ? offsets[island - 1] : Vector3.zero;
            }

            // A Scrambled Seas world is the settings its save keeps plus the island layout they produced; the layout's
            // fingerprint tells worlds apart even if a later version builds the same settings differently. Vanilla
            // when Scrambled Seas is absent or off for this game; null, logged, when its save can't be fully read,
            // so an unreadable world is never written as vanilla or as another world.
            public static WorldInfo World()
            {
                Resolve();
                if (!present) return new WorldInfo();
                // Whether it's on for this game is read here on its own, so a missing mission part can't make a
                // Scrambled Seas world look vanilla.
                if (!(enabled?.GetValue(null) is bool on))
                {
                    Plugin.LogOnce("Voyage Recorder could not tell whether Scrambled Seas is on, so recordings leave the world out.");
                    return null;
                }
                if (!on) return new WorldInfo();
                object container = saveContainer?.GetValue(null, null);
                if (container == null)
                {
                    Plugin.LogOnce("Voyage Recorder could not read Scrambled Seas' world settings, so recordings leave the world out.");
                    return null;
                }
                Type type = container.GetType();
                var missing = new List<string>();
                int Number(string name)
                {
                    if (type.GetProperty(name)?.GetValue(container, null) is int value) return value;
                    missing.Add(name);
                    return 0;
                }
                var offsets = new List<double>();
                foreach (string name in new[] { "archOffsets", "islandOffsets" })
                {
                    if (type.GetProperty(name)?.GetValue(container, null) is Vector3[] vectors)
                        foreach (Vector3 vector in vectors) offsets.AddRange(new double[] { vector.x, vector.y, vector.z });
                    else missing.Add(name);
                }
                var world = new WorldInfo
                {
                    Scrambled = true, Seed = Number("worldScramblerSeed"), IslandSpread = Number("islandSpread"),
                    MinArchipelagoSeparation = Number("minArchipelagoSeparation"), MinIslandSeparation = Number("minIslandSeparation"),
                    LonMin = Number("worldLonMin"), LonMax = Number("worldLonMax"), LatMin = Number("worldLatMin"), LatMax = Number("worldLatMax"),
                    BorderExpander = Number("borderExpander"), Scrambler = Number("version"), ScrambledSeas = version,
                    Layout = LogFormat.LayoutFingerprint(offsets)
                };
                if (missing.Count == 0) return world;
                Plugin.LogOnce("Voyage Recorder could not read Scrambled Seas' " + string.Join(", ", missing) + ", so recordings leave the world out.");
                return null;
            }

            public static string HintKind => Setting("Destination Hint")?.ToString() ?? "";

            // Scrambled Seas rounds to 4, 8 or 16 directions, and to 32 for any other setting.
            public static int Points => Setting("Number of ordinal directions") is int points && (points == 4 || points == 8 || points == 16) ? points : 32;

            public static int Decimals => Setting("Coordinate precision") is int decimals ? Mathf.Clamp(decimals, 0, 6) : 0;

            public static bool HidesDistance => Setting("Hide mission distance") is bool hide && hide;

            public static double HeadingDegrees(Vector3 origin, Vector3 target) =>
                Convert.ToDouble(headingDegrees.Invoke(null, new object[] { origin, target }));

            private static object Setting(string key)
            {
                var definition = new ConfigDefinition("Settings", key);
                return config != null && config.ContainsKey(definition) ? config[definition].BoxedValue : null;
            }
        }
    }
}
