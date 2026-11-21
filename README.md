# Voyage Recorder

A companion mod for [Sailwind Chartroom](https://github.com/Skeptic043/sailwind-chartroom/releases/latest) that records your location so Chartroom can draw your course automatically, and lets you publish your routes and races on [Chartroom Voyages](https://skeptic043.github.io/sailwind-chartroom-voyages/).

## Install

### Mod managers

Install Voyage Recorder through [Sailwind Mod Synchronizer](https://github.com/foxyv/SailwindModSynchronizer/releases/latest), r2modman or Thunderstore Mod Manager, then launch Sailwind through your manager. Dependencies are installed automatically.

### Manual installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) in your Sailwind game folder, following its installation instructions.
2. Download and extract the [Voyage Recorder ZIP](https://github.com/Skeptic043/sailwind-voyage-recorder/releases/latest), then copy its `BepInEx/plugins/VoyageRecorder` folder into `BepInEx/plugins` in your Sailwind folder.
3. Launch Sailwind normally.

## How it works

Press `J` to start or stop recording. The game briefly shows `Recording` or `Stopped recording` to confirm the current state, and you can optionally show a flashing red dot in a corner of the screen while recording. Press `V` to record your current location once. `Shift + V` opens a small menu where you can mark a race start, checkpoint or finish, write a note about where you are, or undo your last mark. In [Sailwind Chartroom](https://github.com/Skeptic043/sailwind-chartroom), choose `Follow live (Voyage Recorder)` to find your saved recordings automatically. Chartroom adds locations to the chart as they are recorded, including the information selected in the configuration. Recordings are stored separately for each save slot in the `Voyage Recorder` folder beside your Sailwind saves. On Windows, this is `%USERPROFILE%\AppData\LocalLow\Raw Lion Workshop\Sailwind\Voyage Recorder`. On Mac, it is under `drive_c/users/<user>/AppData/LocalLow/Raw Lion Workshop/Sailwind/Voyage Recorder` inside your CrossOver, Whisky or other compatibility layer's bottle. To use the recordings in your own tools, see [FORMAT.md](https://github.com/Skeptic043/sailwind-voyage-recorder/blob/main/FORMAT.md) for the file format.

## Configuration

Settings can be changed after the first launch in `BepInEx/config/com.skeptic043.sailwind.voyagerecorder.cfg`, or through [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) while the game is running. Changes take effect immediately.

| Setting | Default | Options |
| --- | --- | --- |
| `Record key` | `J` | The key that starts and stops recording. |
| `Location key` | `V` | The key that records your location once. |
| `Marker menu key` | `Shift + V` | The key that opens the marker menu. |
| `Interval` | `60` | Seconds of game time between positions, from 10 to 600. |
| `Sleep and wake readings` | `true` | Mark your position when you fall asleep and when you wake up. |
| `Record during sleep` | `false` | Keep track of your position while you sleep. |
| `Recording area` | `Anywhere` | `AboardOnly` limits automatic readings to when you're aboard a ship. `AshoreOnly` limits automatic readings to when you're ashore. |
| `Boat` | `true` | Note which boat you are aboard. |
| `Course and speed` | `true` | Note the boat's course and speed. |
| `Wind` | `true` | Note the wind's direction and strength. |
| `Hull damage` | `true` | Note your boat's hull damage. |
| `Port visits` | `true` | Record port arrivals when you tie your boat to a dock cleat, and departures after leaving. |
| `Island landfalls` | `true` | Record when you step ashore with recording active. |
| `Missions` | `true` | Note the direction or coordinates shown by Scrambled Seas for each mission you view. |
| `Show` (Recording dot) | `false` | Show a flashing red dot in a corner of the screen while recording. |
| `Corner` (Recording dot) | `TopRight` | Choose the dot's corner: `TopLeft`, `TopRight`, `BottomLeft` or `BottomRight`. |
| `Size` (Recording dot) | `Small` | `Small` or `Large`. |

## Compatibility

No known incompatibilities. The `Missions` setting only functions in [Scrambled Seas](https://github.com/NANDbrew/scrambled-seas) worlds with its destination hint turned on.

## Credits

- Initially inspired by [Coordinate Viewer](https://github.com/AppSailwindMods/CoordinateViewer)

## AI Use

AI was used to write all of the code in this project. The original concept, design direction, testing, debugging, and release decisions are my own. If you prefer not to use mods developed with AI assistance, I understand and respect that choice.

## Links

[Report an issue](https://github.com/Skeptic043/sailwind-voyage-recorder/issues) · [Source code](https://github.com/Skeptic043/sailwind-voyage-recorder) · [Release notes](https://github.com/Skeptic043/sailwind-voyage-recorder/blob/main/CHANGELOG.md) · [Build instructions](https://github.com/Skeptic043/sailwind-voyage-recorder/blob/main/BUILDING.md) · [License](https://github.com/Skeptic043/sailwind-voyage-recorder/blob/main/LICENSE) · [Support on Ko-fi](https://ko-fi.com/skeptic043) · skeptic043
