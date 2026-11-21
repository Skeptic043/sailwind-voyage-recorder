using System;
using System.Globalization;
using UnityEngine;

namespace VoyageRecorder
{
    internal static partial class Discoveries
    {
        private const string TerraIslandPrefix = "terra incognita ";

        public static LandfallVisit Landfall(IslandHorizon island, int day, double hour, Vector3 playerGlobe)
        {
            bool terra = island.islandIndex == 0;
            Vector3? offset = terra ? null : ScrambledSeas.IslandOffset(island.islandIndex);
            var (prefab, yaw) = terra ? TerraOutline(island) : default;
            string rootName = island.name;
            int labelStart = rootName.IndexOf('(');
            string name = terra ? rootName.Substring(TerraIslandPrefix.Length) : null;
            if (!terra && labelStart >= 0 && rootName.EndsWith(")", StringComparison.Ordinal))
                name = rootName.Substring(labelStart + 1, rootName.Length - labelStart - 2);

            Vector3 islandGlobe = FloatingOriginManager.instance.GetGlobeCoords(island.transform);
            return new LandfallVisit
            {
                Day = day, Hour = hour,
                Source = terra ? "terra-incognita" : "native",
                IslandId = terra ? rootName : island.islandIndex.ToString(CultureInfo.InvariantCulture),
                Name = name,
                Lat = playerGlobe.z, Lon = playerGlobe.x,
                IslandLat = islandGlobe.z, IslandLon = islandGlobe.x,
                OffsetLat = offset?.z / 9000.0, OffsetLon = offset?.x / 9000.0,
                Prefab = prefab, Yaw = yaw
            };
        }

        private static (string Prefab, int? Yaw) TerraOutline(IslandHorizon island)
        {
            // Terra Incognita renames the island root but keeps the prefab name in its rotated terrain data.
            Terrain terrain = island.GetComponentInChildren<Terrain>(true);
            TerrainData data = terrain != null ? terrain.terrainData : null;
            if (data == null) return default;
            const string suffix = "_terrain rot";
            string name = data.name;
            int split = name.LastIndexOf(suffix, StringComparison.Ordinal);
            if (split <= 0 || !int.TryParse(name.Substring(split + suffix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int yaw) ||
                yaw < 0 || yaw > 270 || yaw % 90 != 0) return default;
            return (name.Substring(0, split), yaw);
        }

        public static IslandHorizon ConfirmLandfall(IslandLandfalls visits, bool manual)
        {
            if (GameState.currentBoat != null || PlayerSwimming.swimming)
            {
                visits.ObserveOffIsland();
                return null;
            }
            if (PlayerSwimming.inShallowWater)
            {
                visits.ObserveShallows();
                return null;
            }

            IslandHorizon island = IslandUnderfoot();
            if (island == null)
            {
                visits.ClearPending();
                return null;
            }

            return visits.ObserveIsland(island.GetInstanceID(), manual) ? island : null;
        }

        internal static IslandHorizon IslandUnderfoot()
        {
            // The local controller reports contact. Sailwind's footsteps already sample the surface below it.
            CharacterController controller = Refs.charController;
            if (controller == null || !controller.enabled || !controller.isGrounded || GameState.onRatlines) return null;
            FootstepsAudio footsteps = controller.GetComponent<FootstepsAudio>();
            if (footsteps == null || !footsteps.isActiveAndEnabled) return null;

            Collider ground = FootstepsAudio.currentGroundCol;
            if (ground == null || !ground.enabled || ground.isTrigger || !ground.gameObject.activeInHierarchy) return null;

            IslandHorizon island = ground.GetComponentInParent<IslandHorizon>();
            if (island != null && (RegisteredIsland(island.islandIndex) == island.transform ||
                (island.islandIndex == 0 && island.name.StartsWith(TerraIslandPrefix, StringComparison.Ordinal)))) return island;

            // Additive scenery follows its island without being parented to its horizon root.
            IslandSceneryScene scenery = ground.GetComponentInParent<IslandSceneryScene>();
            Transform owner = scenery != null ? RegisteredIsland(scenery.parentIslandIndex) : null;
            return owner != null ? owner.GetComponent<IslandHorizon>() : null;
        }

        private static Transform RegisteredIsland(int index)
        {
            Transform[] islands = Refs.islands;
            return islands != null && index > 0 && index < islands.Length ? islands[index] : null;
        }

        /// <summary>The registered island that owns the port, or -1.</summary>
        public static int IslandOf(Port port)
        {
            if (port == null) return -1;
            IslandHorizon island = port.GetComponentInParent<IslandHorizon>();
            if (island != null && RegisteredIsland(island.islandIndex) == island.transform) return island.islandIndex;

            island = port.island != null ? port.island.GetComponentInParent<IslandHorizon>() : null;
            return island != null && RegisteredIsland(island.islandIndex) == island.transform ? island.islandIndex : -1;
        }

        public static Port PortAtCleat(GPButtonDockMooring cleat)
        {
            Port parent = cleat.GetComponentInParent<Port>();
            if (parent != null && parent.isActiveAndEnabled) return parent;

            // Stock cleats usually sit beside the port under the island root.
            IslandHorizon island = cleat.GetComponentInParent<IslandHorizon>();
            if (island == null || RegisteredIsland(island.islandIndex) != island.transform || Port.ports == null) return null;

            Port match = null;
            Port recoveryMatch = null;
            bool multiple = false;
            foreach (Port port in Port.ports)
            {
                if (port == null || !port.isActiveAndEnabled || IslandOf(port) != island.islandIndex) continue;
                multiple |= match != null;
                match = port;
                // Neverdin also has a test Port without a registered recovery marker.
                RecoveryPort recovery = Recovery.GetPort(port.portIndex);
                if (recovery == null || recovery.parentPort != port) continue;
                if (recoveryMatch != null) return null;
                recoveryMatch = port;
            }
            if (recoveryMatch != null) return recoveryMatch;
            return multiple ? null : match;
        }
    }
}
