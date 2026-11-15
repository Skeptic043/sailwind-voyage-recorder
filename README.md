# Voyage Recorder

Companion mod for [Sailwind Chartroom](https://github.com/Skeptic043/sailwind-chartroom/releases/latest). Records your location for Chartroom to draw your course automatically.

## Install

### Mod managers

Install Voyage Recorder through [Sailwind Mod Synchronizer](https://github.com/foxyv/SailwindModSynchronizer/releases/latest), then launch Sailwind through it.

### Manual installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) in your Sailwind game folder, following its installation instructions.
2. Download and extract the [Voyage Recorder ZIP](https://github.com/Skeptic043/sailwind-voyage-recorder/releases/latest). Copy its `BepInEx/plugins/VoyageRecorder` folder into `BepInEx/plugins` in your Sailwind folder.
3. Launch Sailwind normally.

## How it works

Press `J` to start recording and again to stop, and the game briefly shows `Recording` or `Stopped recording` to confirm which it is, with an optional flashing red dot in a corner of the screen while you're recording. Pressing `V` records your current location once. Voyage Recorder automatically records your location every 60 seconds of game time and has various configurable options to adjust to your preference. In [Sailwind Chartroom](https://github.com/Skeptic043/sailwind-chartroom), choosing `Follow live (Voyage Recorder)` automatically finds saved recordings to choose from, and Chartroom adds to its chart as locations are recorded, including the information selected in the config, the ports you stop at and a search area for each mission you look at. Recordings are kept per save slot in the `Voyage Recorder` folder next to your Sailwind saves, `%USERPROFILE%\AppData\LocalLow\Raw Lion Workshop\Sailwind\Voyage Recorder` on Windows, or under `drive_c/users/<user>/AppData/LocalLow/Raw Lion Workshop/Sailwind/Voyage Recorder` inside your CrossOver, Whisky or other compatibility layer's bottle on Mac. If you'd like to use the recordings in your own tools, [FORMAT.md](https://github.com/Skeptic043/sailwind-voyage-recorder/blob/main/FORMAT.md) describes the format.

## Configuration

Settings can be adjusted in `BepInEx/config/com.skeptic043.sailwind.voyagerecorder.cfg` after the first launch, or in game with [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager). Changes apply immediately.

| Setting | Default | Options |
| --- | --- | --- |
| `Record key` | `J` | The key that starts and stops recording. |
| `Location key` | `V` | The key that records your location once. |
| `Interval` | `60` | Seconds of game time between positions, from 10 to 600. |
| `Sleep and wake readings` | `true` | Also note your position when you fall asleep and when you wake up. |
| `Record during sleep` | `false` | Keep noting your position while you sleep. |
| `Only while aboard` | `false` | Skip positions while you're not aboard a boat. |
| `Boat` | `true` | Note which boat you're aboard. |
| `Course and speed` | `true` | Note the boat's course and speed. |
| `Wind` | `true` | Note the wind's direction and strength. |
| `Hull damage` | `true` | Note your boat's hull damage. |
| `Port visits` | `true` | Note each port you stop at. |
| `Missions` | `true` | Note the direction or coordinates Scrambled Seas shows for the missions you look at. |
| `Show` (Recording dot) | `false` | Show a flashing red dot in a corner of the screen while recording. |
| `Corner` (Recording dot) | `TopRight` | Which corner the dot sits in: `TopLeft`, `TopRight`, `BottomLeft` or `BottomRight`. |
| `Size` (Recording dot) | `Small` | `Small` or `Large`. |

## Compatibility

No known incompatibilities. Mission search areas need Scrambled Seas with its destination hint turned on.

## Credits

- Initially inspired by [Coordinate Viewer](https://github.com/AppSailwindMods/CoordinateViewer)

## AI Use

AI was used to write all of the code in this project. The original concept, design direction, testing, debugging, and release decisions are my own. If you prefer not to use mods developed with AI assistance, I understand and respect that choice.

## Links

[Report an issue](https://github.com/Skeptic043/sailwind-voyage-recorder/issues) · [Source code](https://github.com/Skeptic043/sailwind-voyage-recorder) · [Release notes](https://github.com/Skeptic043/sailwind-voyage-recorder/blob/main/CHANGELOG.md) · [Build instructions](https://github.com/Skeptic043/sailwind-voyage-recorder/blob/main/BUILDING.md) · [License](https://github.com/Skeptic043/sailwind-voyage-recorder/blob/main/LICENSE) · [Support on Ko-fi](https://ko-fi.com/skeptic043) · skeptic043
