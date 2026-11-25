using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using UnityEngine;

namespace VoyageRecorder
{
    public sealed partial class Plugin
    {
        private readonly List<string> portLines = new List<string>();
        private static bool CanRead => true;
        private bool recording = true;
        private readonly PortSettings recordPorts = new PortSettings();
        private readonly PortSettings portlessHints = new PortSettings { Value = false };
        private readonly PortSettings secretHints = new PortSettings { Value = false };
        private readonly HashSet<string> writtenMissions = new HashSet<string>();
        private sealed class PortSettings { public bool Value = true; }
        private void Write(string line, int day, double hour) => portLines.Add(line);
        private static Transform BoatRoot(Transform boat) => boat.parent ?? boat;
        private static void ReadingFailed(Exception exception) => throw exception;

        internal static void CheckPortVisits(Action<bool, string> check)
        {
            var recorder = new Plugin();
            Refs.islands = new Transform[34];
            var eastwind = new Transform();
            eastwind.Add(new IslandHorizon { islandIndex = 19 });
            Refs.islands[19] = eastwind;
            var grotto = new Transform();
            grotto.Add(new IslandHorizon { islandIndex = 33 });
            Refs.islands[33] = grotto;
            Port eastwindPort = new Transform { name = "Eastwind", parent = eastwind, position = new Vector3(325) }.Add(new Port { portIndex = 19 });
            Port grottoPort = new Transform { name = "Firefly Grotto", parent = grotto }.Add(new Port { portIndex = 28 });
            Port.ports = new[] { eastwindPort, grottoPort };

            var boat = new Transform();
            Rigidbody body = boat.Add(new Rigidbody());
            GameState.currentBoat = new Transform { parent = boat };
            GameState.lastBoat = boat;
            Refs.observerMirror = new Transform();
            GameState.lastVisitedPort = eastwindPort;
            recorder.RecordPortDeparture();
            check(recorder.portLines.Count == 0, "an untied stop 325 metres from Eastwind does not write a port visit");

            GameState.lastVisitedPort = null;
            recorder = new Plugin();
            var dock = new Transform { parent = grotto };
            var cleat = dock.Add(new GPButtonDockMooring { spring = dock.Add(new SpringJoint { connectedBody = body }) });
            var rope = new PickupableBoatMooringRope { body = body };
            cleat.spring.connectedBody = new Rigidbody();
            recorder.RecordMooring(rope, cleat);
            check(recorder.portLines.Count == 0, "a cleat without a spring connection to this boat does not record an arrival");
            cleat.spring.connectedBody = body;

            recorder.recording = false;
            recorder.RecordMooring(rope, cleat);
            check(recorder.portLines.Count == 0, "tying up with recording off does not record an arrival");
            recorder.recording = true;
            recorder.RecordMooring(rope, cleat);
            check(recorder.portLines.Count == 1 && RecordIs(recorder.portLines[0], "port", "Firefly Grotto", 33),
                "a connected dock line records the cleat's port before the game reports it nearby");

            var secondDock = new Transform { parent = grotto };
            var secondCleat = secondDock.Add(new GPButtonDockMooring { spring = secondDock.Add(new SpringJoint { connectedBody = body }) });
            recorder.RecordMooring(rope, secondCleat);
            check(recorder.portLines.Count == 1, "tying a second line at the same port does not duplicate the visit");

            GameState.currentBoat = null;
            GameState.lastVisitedPort = grottoPort;
            var eastwindDock = new Transform { parent = eastwind };
            var eastwindCleat = eastwindDock.Add(new GPButtonDockMooring { spring = eastwindDock.Add(new SpringJoint { connectedBody = body }) });
            recorder.RecordMooring(rope, eastwindCleat);
            check(recorder.portLines.Count == 3 && RecordIs(recorder.portLines[1], "depart", "Firefly Grotto", 33) &&
                RecordIs(recorder.portLines[2], "port", "Eastwind", 19),
                "tying from the dock records the new port and closes the previous visit even if the ports are close");

            Refs.observerMirror.position = new Vector3(2000);
            recorder.RecordPortDeparture();
            check(recorder.portLines.Count == 4 && RecordIs(recorder.portLines[3], "depart", "Eastwind", 19),
                "an ordinary timed reading still records departure after leaving port");

            var alternatePort = new Transform { name = "Test Port", parent = eastwind }.Add(new Port { portIndex = 7 });
            Port.ports = new[] { alternatePort, eastwindPort, grottoPort };
            Recovery.Register(eastwindPort);
            check(Discoveries.PortAtCleat(eastwindCleat) == eastwindPort, "a registered recovery port takes precedence over a test port on the same island");

            CheckCapitalHints(check, body, rope);
        }

        private static void CheckCapitalHints(Action<bool, string> check, Rigidbody body, PickupableBoatMooringRope rope)
        {
            Refs.islands = new Transform[43];
            Transform Island(int number, float x)
            {
                var root = new Transform { name = "island " + number, position = new Vector3(x) };
                root.Add(new IslandHorizon { islandIndex = number });
                Refs.islands[number] = root;
                return root;
            }
            Transform goldRock = Island(1, 0);
            Island(5, 0.5f);
            Island(6, -0.2f);
            Island(32, 1);
            Island(36, 12);
            Island(42, 2);
            Transform fort = Island(15, 10);
            Port goldRockPort = new Transform { name = "Gold Rock City", parent = goldRock }.Add(new Port { portIndex = 0 });
            Port fortPort = new Transform { name = "Fort Aestrin", parent = fort }.Add(new Port { portIndex = 15 });
            Port.ports = new[] { goldRockPort, fortPort };
            GameState.currentBoat = null;
            List<string> Destinations(Plugin plugin) => plugin.portLines.Select(line => JsonDocument.Parse(line).RootElement)
                .Where(line => line.GetProperty("type").GetString() == "mission").Select(line => line.GetProperty("destination").GetString()).ToList();

            var recorder = new Plugin();
            recorder.WriteCapitalHints(new[] { goldRockPort }, 1, 8);
            check(recorder.portLines.Count == 0, "a capital's mission list writes no island hints with the setting off");

            var dock = new Transform { parent = goldRock };
            recorder.portlessHints.Value = true;
            recorder.RecordMooring(rope, dock.Add(new GPButtonDockMooring { spring = dock.Add(new SpringJoint { connectedBody = body }) }));
            check(recorder.portLines.Count == 1, "tying up at a capital records the visit but no hints");

            recorder.WriteCapitalHints(new[] { goldRockPort }, 1, 8);
            var hints = recorder.portLines.Skip(1).Select(line => JsonDocument.Parse(line).RootElement).ToList();
            check(hints.Count == 2 && hints.All(hint => hint.GetProperty("type").GetString() == "mission" &&
                    hint.GetProperty("source").GetString() == "capital" && hint.GetProperty("origin").GetString() == "Gold Rock City" &&
                    hint.GetProperty("points").GetInt32() == 32 && hint.TryGetProperty("heading", out _) && hint.TryGetProperty("miles", out _)) &&
                Destinations(recorder).SequenceEqual(new[] { "Isle of Clear Mind", "Lion's Fang" }),
                "Gold Rock City's mission list hints toward each Al'Ankh island without a port, on the 32-point compass without Scrambled Seas");

            recorder.WriteCapitalHints(new[] { goldRockPort, fortPort }, 1, 9);
            check(Destinations(recorder).SequenceEqual(new[] { "Isle of Clear Mind", "Lion's Fang" }),
                "secret islands stay hidden, and a capital's hints are written once a session");

            recorder.secretHints.Value = true;
            recorder.WriteCapitalHints(new[] { goldRockPort, fortPort }, 1, 10);
            check(Destinations(recorder).SequenceEqual(new[] { "Isle of Clear Mind", "Lion's Fang", "Eleann Island", "Saffron", "Bone Isle" }),
                "showing secret islands adds Eleann Island, Saffron and Bone Isle");
        }

        private static bool RecordIs(string line, string type, string port, int island)
        {
            using var json = JsonDocument.Parse(line);
            JsonElement value = json.RootElement;
            return value.GetProperty("type").GetString() == type && value.GetProperty("port").GetString() == port &&
                value.GetProperty("island").GetInt32() == island;
        }
    }

    internal static partial class Discoveries
    {
        internal static PortVisit Visit(Port port, int day, double hour) =>
            new PortVisit { Port = port.GetPortName(), Island = IslandOf(port), Day = day, Hour = hour };
    }
}
