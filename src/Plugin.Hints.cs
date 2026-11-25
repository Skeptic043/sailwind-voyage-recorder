using System.Collections.Generic;

namespace VoyageRecorder
{
    public sealed partial class Plugin
    {
        // When a mission list shows, each capital it has missions from gives its island hints, once a session for each
        // island: Chartroom keeps an area until the island is found. A secret island's hint waits until secret
        // islands are shown.
        private void WriteCapitalHints(IEnumerable<Port> origins, int day, double hour)
        {
            if (!portlessHints.Value) return;
            foreach (Port origin in origins)
            {
                if (origin == null) continue;
                foreach (MissionHint hint in Discoveries.CapitalHints(origin, day, hour, secretHints.Value))
                    if (writtenMissions.Add("capital>" + hint.DestinationIsland)) Write(LogFormat.Mission(hint), day, hour);
            }
        }
    }
}
