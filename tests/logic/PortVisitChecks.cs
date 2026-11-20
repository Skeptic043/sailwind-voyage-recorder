using System;
using System.Collections.Generic;
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
