using System;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;

namespace VoyageRecorder
{
    internal static class IslandContacts
    {
        public static void Check(Action<bool, string> check)
        {
            var malefic = new Transform { position = new Vector3(800) };
            IslandHorizon owner = malefic.Add(new IslandHorizon { islandIndex = 17 });
            var grotto = new Transform { position = new Vector3(10) };
            grotto.Add(new IslandHorizon { islandIndex = 33 });
            Refs.islands = new Transform[34];
            Refs.islands[17] = malefic;
            Refs.islands[33] = grotto;

            var portSite = new Transform { parent = malefic };
            IslandEconomy economy = malefic.Add(new IslandEconomy());
            Port port = portSite.Add(new Port { island = economy });
            check(Discoveries.IslandOf(port) == 17, "Mount Malefic port belongs to island 17 with Firefly Grotto's centre closer");
            portSite.parent = null;
            check(Discoveries.IslandOf(port) == 17, "a port outside the hierarchy resolves through its assigned economy");
            port.island = null;
            check(Discoveries.IslandOf(port) == -1, "a port without an owner does not claim an adjacent island");

            var player = new Transform();
            Refs.charController = player.Add(new CharacterController());
            player.Add(new FootstepsAudio());
            var terrain = new Transform { parent = malefic };
            FootstepsAudio.currentGroundCol = terrain.Add(new Collider());

            var visits = new IslandLandfalls();
            PlayerSwimming.inShallowWater = true;
            check(Discoveries.ConfirmLandfall(visits, manual: false) == null, "starting in shallow water does not immediately count as landfall");
            PlayerSwimming.inShallowWater = false;
            check(Discoveries.ConfirmLandfall(visits, manual: false) == null, "the first dry sample is unconfirmed");
            check(Discoveries.ConfirmLandfall(visits, manual: false) == owner, "starting in shallow water records a landfall after two dry samples");

            visits.Recorded(owner.GetInstanceID());
            PlayerSwimming.inShallowWater = true;
            check(Discoveries.ConfirmLandfall(new IslandLandfalls(), manual: true) == null, "the location key cannot record landfall in shallow water");
            Discoveries.ConfirmLandfall(visits, manual: false);
            PlayerSwimming.inShallowWater = false;
            check(Discoveries.ConfirmLandfall(visits, manual: true) == null, "wading at the current island does not end an already recorded visit");
            check(Discoveries.ConfirmLandfall(new IslandLandfalls(), manual: true) == owner, "the location key can record landfall immediately once dry");

            var generated = new Transform { name = "terra incognita SeaStacks (scatter:archipelago0@(41000,-43000)) #1" };
            IslandHorizon terra = generated.Add(new IslandHorizon());
            var tile = new Transform { parent = generated };
            generated.children.Add(tile);
            var terrainAsset = tile.Add(new Terrain { terrainData = new TerrainData { name = "stacks_13_terrain rot270" } });
            using var confirmed = JsonDocument.Parse(LogFormat.Landfall(Discoveries.Landfall(terra, 1, 8, new Vector3(3))));
            check(confirmed.RootElement.GetProperty("islandId").GetString() == generated.name,
                "the generated instance keeps its own landfall identity");
            check(confirmed.RootElement.TryGetProperty("prefab", out JsonElement prefab) && prefab.GetString() == "stacks_13" &&
                confirmed.RootElement.TryGetProperty("yaw", out JsonElement yaw) && yaw.GetInt32() == 270,
                "a SeaStacks landfall records the actual terrain variant and rotation");
            terrainAsset.terrainData.name = "unrelated terrain";
            using var unknown = JsonDocument.Parse(LogFormat.Landfall(Discoveries.Landfall(terra, 1, 8, new Vector3(3))));
            check(!unknown.RootElement.TryGetProperty("prefab", out _) && !unknown.RootElement.TryGetProperty("yaw", out _),
                "unrecognized terrain still records a landfall without claiming an outline");
        }
    }

    internal static partial class Discoveries
    {
        private static class ScrambledSeas
        {
            internal static Vector3? IslandOffset(int island) => null;
        }
    }
}

// Minimal native surface for running the production contact and ownership decisions without Unity.
namespace UnityEngine
{
    internal struct Vector3
    {
        public readonly float x;
        public readonly float z;
        public Vector3(float x, float z = 0) { this.x = x; this.z = z; }
        public static Vector3 operator -(Vector3 left, Vector3 right) => new Vector3(left.x - right.x);
        public static float Distance(Vector3 left, Vector3 right) => Math.Abs(left.x - right.x);
        public float sqrMagnitude => x * x;
    }

    internal class Component
    {
        public Transform transform;
        public string name => transform.name;
        public GameObject gameObject => transform.gameObject;
        public T GetComponent<T>() where T : Component => transform.Find<T>();
        public T GetComponentInChildren<T>(bool includeInactive) where T : Component
        {
            T match = GetComponent<T>();
            if (match != null) return match;
            foreach (Transform child in transform.children)
            {
                match = child.GetComponentInChildren<T>(includeInactive);
                if (match != null) return match;
            }
            return null;
        }

        public T GetComponentInParent<T>() where T : Component
        {
            for (Transform node = transform; node != null; node = node.parent)
            {
                T match = node.Find<T>();
                if (match != null) return match;
            }
            return null;
        }
    }

    internal sealed class Transform : Component
    {
        private readonly Dictionary<Type, Component> components = new Dictionary<Type, Component>();
        public Transform() { transform = this; }
        public new string name = "";
        public new GameObject gameObject = new GameObject();
        public Transform parent;
        public readonly List<Transform> children = new List<Transform>();
        public Vector3 position;

        public T Add<T>(T component) where T : Component
        {
            component.transform = this;
            components.Add(typeof(T), component);
            return component;
        }

        public T Find<T>() where T : Component => components.TryGetValue(typeof(T), out Component component) ? (T)component : null;
    }

    internal sealed class GameObject { public bool activeInHierarchy = true; }
    internal sealed class Rigidbody : Component { }
    internal sealed class SpringJoint : Component { public Rigidbody connectedBody; }
    internal sealed class Terrain : Component { public TerrainData terrainData; }
    internal sealed class TerrainData { public string name; }
    internal sealed class CharacterController : Component
    {
        public bool enabled = true;
        public bool isGrounded = true;
    }

    internal sealed class Collider : Component
    {
        public bool enabled = true;
        public bool isTrigger = false;
    }
}

internal static class Refs
{
    public static Transform[] islands;
    public static CharacterController charController;
    public static Transform observerMirror;
}

internal static class GameState
{
    public static Transform currentBoat = null;
    public static Transform lastBoat;
    public static Port lastVisitedPort;
    public static int day = 0;
    public static bool onRatlines = false;
    public static bool justStarted = false;
    public static bool recovering = false;
    public static bool sleeping = false;
}

internal sealed class FloatingOriginManager
{
    public static FloatingOriginManager instance = new FloatingOriginManager();
    public Vector3 GetGlobeCoords(Transform transform) => transform.position;
}

internal static class PlayerSwimming
{
    public static bool swimming = false;
    public static bool inShallowWater;
}
internal sealed class FootstepsAudio : Component
{
    public static Collider currentGroundCol;
    public bool isActiveAndEnabled = true;
}

internal sealed class IslandHorizon : Component
{
    public int islandIndex;
    public int GetInstanceID() => islandIndex;
}
internal sealed class IslandSceneryScene : Component { public int parentIslandIndex = 0; }
internal sealed class IslandEconomy : Component { }
internal sealed class Port : Component
{
    public static Port[] ports;
    public IslandEconomy island;
    public int portIndex;
    public bool isActiveAndEnabled = true;
    public string GetPortName() => name;
}

internal sealed class PickupableBoatMooringRope : Component
{
    public Rigidbody body;
    public Rigidbody GetBoatRigidbody() => body;
}

internal sealed class GPButtonDockMooring : Component { public SpringJoint spring; }
internal sealed class Sun { public static Sun sun = new Sun(); public double globalTime = 6; }
internal sealed class RecoveryPort { public Port parentPort; }

internal static class Recovery
{
    private static readonly Dictionary<int, RecoveryPort> ports = new Dictionary<int, RecoveryPort>();
    public static void Register(Port port) => ports.Add(port.portIndex, new RecoveryPort { parentPort = port });
    public static RecoveryPort GetPort(int index) => ports.TryGetValue(index, out RecoveryPort port) ? port : null;
}
