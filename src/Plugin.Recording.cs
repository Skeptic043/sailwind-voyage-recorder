using UnityEngine;

namespace VoyageRecorder
{
    public sealed partial class Plugin
    {
        internal enum RecordingArea { Anywhere, AboardOnly, AshoreOnly }

        internal static bool AllowsPosition(RecordingArea area, Transform boat, bool manual = false, bool wakingInTavern = false)
        {
            if (area == RecordingArea.AshoreOnly)
            {
                return boat == null && !GameState.recovering && !PlayerSwimming.swimming && !PlayerSwimming.inShallowWater &&
                    ((!manual && (wakingInTavern || (GameState.sleeping && GameState.sleepingInTavern))) ||
                     Discoveries.IslandUnderfoot() != null);
            }

            // The old aboard-only setting only filtered automatic readings.
            return area != RecordingArea.AboardOnly || manual || boat != null;
        }
    }
}
