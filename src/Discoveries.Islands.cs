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
            if (island.islandIndex < 0) return UnnumberedLandfall(island, day, hour, playerGlobe);
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

        // Ground the game gives no island number, such as the Fire Fish Lagoon islets: one landmass under
        // "island 26 lagoon parent terrain (-1)", which Scrambled Seas moves with the Lagoon archipelago rather than
        // with any numbered island. Its root's name and position say which piece of land it is and where it went.
        private static LandfallVisit UnnumberedLandfall(IslandHorizon island, int day, double hour, Vector3 playerGlobe)
        {
            Vector3 rootGlobe = FloatingOriginManager.instance.GetGlobeCoords(island.transform);
            return new LandfallVisit
            {
                Day = day, Hour = hour, Source = "unnumbered", IslandId = island.name,
                Lat = playerGlobe.z, Lon = playerGlobe.x, IslandLat = rootGlobe.z, IslandLon = rootGlobe.x
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
            bool offIsland = GameState.currentBoat != null || PlayerSwimming.swimming;
            if (offIsland || PlayerSwimming.inShallowWater)
            {
                // Few players step onto the lagoon's small islands, so passing close by counts.
                IslandHorizon near = LagoonIslandNearby();
                if (near != null) return Observe(visits, near, manual);
                if (offIsland) visits.ObserveOffIsland();
                else visits.ObserveShallows();
                return null;
            }

            IslandHorizon island = IslandUnderfoot() ?? LagoonIslandNearby();
            if (island == null)
            {
                visits.ClearPending();
                return null;
            }
            return Observe(visits, island, manual);
        }

        private static IslandHorizon Observe(IslandLandfalls visits, IslandHorizon island, bool manual)
        {
            // On an island with islets, getting this far from where its landfall was recorded is a landing on
            // another islet, so the visit starts again. Chartroom works out the islet from the position.
            if (Array.IndexOf(IslandsWithIslets, island.islandIndex) >= 0)
            {
                Vector3 globe = FloatingOriginManager.instance.GetGlobeCoords(Refs.observerMirror.transform);
                if (visits.FarFromLanding(island.GetInstanceID(), globe.x, globe.z, IsletDegrees)) visits.ObserveOffIsland();
            }
            return visits.ObserveIsland(island.GetInstanceID(), manual) ? island : null;
        }

        // Fire Fish Town, Kicia Bay and Sen'na each include islets across the lagoon, and the lagoon's sandbars
        // share one root numbered -1. Their islets are 400 metres or more apart.
        private static readonly int[] IslandsWithIslets = { -1, 26, 27, 28 };
        private const double IsletDegrees = 0.03;

        // The Fire Fish Lagoon's islands and sandbars. Within this distance of their dry land counts as a landfall,
        // aboard, swimming or wading.
        private static readonly int[] LagoonIslands = { -1, 26, 27, 28, 29, 31 };
        private const float NearbyMetres = 60f;
        // Room for the player's boat, its cargo and a port's buildings as well as the island's own colliders.
        private static readonly Collider[] nearby = new Collider[256];

        // A lagoon island with land at least half a metre above the sea within NearbyMetres of the player: a flat box
        // from 0.5 to 10 metres above sea level, so the seabed around it never counts.
        internal static IslandHorizon LagoonIslandNearby()
        {
            Transform player = Refs.observerMirror != null ? Refs.observerMirror.transform : null;
            if (player == null) return null;
            Vector3 at = player.position;
            int count = Physics.OverlapBoxNonAlloc(new Vector3(at.x, 5.25f, at.z), new Vector3(NearbyMetres, 4.75f, NearbyMetres),
                nearby, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider ground = nearby[i];
                nearby[i] = null;
                if (ground == null || !ground.enabled) continue;
                IslandHorizon island = ground.GetComponentInParent<IslandHorizon>();
                if (island == null || Array.IndexOf(LagoonIslands, island.islandIndex) < 0) continue;
                if (island.islandIndex < 0 || RegisteredIsland(island.islandIndex) == island.transform)
                {
                    for (int rest = i + 1; rest < count; rest++) nearby[rest] = null;
                    return island;
                }
            }
            return null;
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
            if (island != null && (RegisteredIsland(island.islandIndex) == island.transform || island.islandIndex < 0 ||
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
