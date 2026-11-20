using System;
using UnityEngine;

namespace VoyageRecorder
{
    public sealed partial class Plugin
    {
        private Port atPort;
        private const float PortDepartureMetres = 1500f;

        private bool HasLeftPort() =>
            atPort != null && Vector3.Distance(atPort.transform.position, Refs.observerMirror.transform.position) > PortDepartureMetres;

        private void RecordPortDeparture()
        {
            if (HasLeftPort()) WritePortDeparture(GameState.day, Sun.sun.globalTime);
        }

        private void WritePortDeparture(int day, double hour)
        {
            Write(LogFormat.Depart(new PortDeparture { Day = day, Hour = hour, Port = atPort.GetPortName(), Island = Discoveries.IslandOf(atPort) }), day, hour);
            atPort = null;
        }

        private void RecordMooring(PickupableBoatMooringRope rope, GPButtonDockMooring cleat)
        {
            if (!recording || !recordPorts.Value || !CanRead || GameState.justStarted || GameState.recovering || GameState.sleeping) return;

            try
            {
                // The player can tie a line from the dock after stepping off their boat.
                Transform boat = GameState.currentBoat != null ? BoatRoot(GameState.currentBoat) : GameState.lastBoat;
                Rigidbody body = rope.GetBoatRigidbody();
                if (body == null || body.transform != boat || cleat == null || cleat.spring == null || cleat.spring.connectedBody != body) return;

                Port port = Discoveries.PortAtCleat(cleat);
                if (port == null || port == atPort) return;
                int day = GameState.day;
                double hour = Sun.sun.globalTime;
                // Two ports can be close enough that the next docking comes before the distance-based departure.
                if (atPort != null) WritePortDeparture(day, hour);
                Write(LogFormat.Port(Discoveries.Visit(port, day, hour)), day, hour);
                atPort = port;
            }
            catch (Exception exception)
            {
                ReadingFailed(exception);
            }
        }
    }
}
