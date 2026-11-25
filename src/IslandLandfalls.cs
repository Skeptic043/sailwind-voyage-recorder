using System;

namespace VoyageRecorder
{
    /// <summary>Confirms visits within the current session using the live island roots' instance IDs.</summary>
    internal sealed class IslandLandfalls
    {
        private bool needsBaseline = true;
        private int? currentIsland;
        private bool currentVisitRecorded;
        private int? pendingIsland;
        // Where the current visit's landfall was recorded, in globe degrees.
        private double landedLon = double.NaN;
        private double landedLat = double.NaN;

        public void Reset()
        {
            currentIsland = null;
            currentVisitRecorded = false;
            Rebaseline();
        }

        // After a gap, keep a recorded visit if the player is still on the same island.
        public void Rebaseline()
        {
            needsBaseline = true;
            ClearPending();
        }

        public void ObserveOffIsland()
        {
            needsBaseline = false;
            currentIsland = null;
            currentVisitRecorded = false;
            ClearPending();
        }

        // Starting in water should allow a new landing. Brief wading keeps the current visit.
        public void ObserveShallows()
        {
            needsBaseline = false;
            ClearPending();
        }

        // A jump or an unresolved surface interrupts confirmation without ending the visit.
        public void ClearPending() => pendingIsland = null;

        public bool ObserveIsland(int island, bool manual = false)
        {
            if (needsBaseline)
            {
                needsBaseline = false;
                if (currentIsland != island) currentVisitRecorded = false;
                currentIsland = island;
            }
            if (currentIsland == island)
            {
                ClearPending();
                return manual && !currentVisitRecorded;
            }
            bool confirmed = manual || pendingIsland == island;
            pendingIsland = island;
            return confirmed;
        }

        // Commit the visit only after its line has been appended.
        // Whether the player is this far from where the current visit's landfall on this island was recorded.
        public bool FarFromLanding(int island, double lon, double lat, double degrees) =>
            currentVisitRecorded && currentIsland == island && Math.Sqrt((lon - landedLon) * (lon - landedLon) + (lat - landedLat) * (lat - landedLat)) > degrees;

        public void Recorded(int island, double lon = double.NaN, double lat = double.NaN)
        {
            landedLon = lon;
            landedLat = lat;
            currentIsland = island;
            currentVisitRecorded = true;
            ClearPending();
        }
    }
}
