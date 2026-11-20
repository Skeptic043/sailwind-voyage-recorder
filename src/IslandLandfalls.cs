namespace VoyageRecorder
{
    /// <summary>Confirms visits within the current session using the live island roots' instance IDs.</summary>
    internal sealed class IslandLandfalls
    {
        private bool needsBaseline = true;
        private int? currentIsland;
        private bool currentVisitRecorded;
        private int? pendingIsland;

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
        public void Recorded(int island)
        {
            currentIsland = island;
            currentVisitRecorded = true;
            ClearPending();
        }
    }
}
