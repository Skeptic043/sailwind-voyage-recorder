# Changelog

## 0.2.8

### Added
- While recording, Voyage Recorder logs landfalls when you step onto dry island ground. With `Island landfalls` enabled, press the Location key (`V` by default) while ashore to record a landfall
- Recordings of the exact variant and orientation of a Terra Incognita landfall. These details let [Sailwind Chartroom 0.9.26](https://github.com/Skeptic043/sailwind-chartroom/releases/latest) place the matching outline

### Changed
- Port arrivals are now recorded when you tie your boat to a dock cleat instead of when you enter the port's area

### Fixed
- Scrambled Seas islands that are placed close together now use the correct name if both are automatically registered at the same time. Previously, both would incorrectly have the same name

## 0.2.2

### Added
- Press `Shift + V` to mark a race start, checkpoint or finish, write a note about where you are, or undo your last mark
- Every port visit is now recorded, including returns to ports already visited, along with the rough time you leave
- Recordings now include real time, the world you are sailing in (vanilla or your Scrambled Seas world), and the loaded mods for comparing and sharing voyages in Sailwind Chartroom

## 0.1.9

### Added
- Recordings note which game they belong to, so Sailwind Chartroom keeps a new game in the same save slot apart from the old one

## 0.1.8

### Initial Release
- Press `J` to record your voyage for Sailwind Chartroom to follow live. Records once every 60 seconds of game time by default, adjustable in the config
- Press `V` to record your location once
- Keybinds are adjustable in the config
- An optional flashing red dot shows you're recording, in the corner and size of your choice
- Trackable items (toggleable in the config) include: current boat, the boat's course and speed, wind direction and strength, hull damage, ports you visit for Chartroom to mark automatically, and missions for Chartroom to draw `Search area` markers automatically
- Recordings are saved in `%USERPROFILE%\AppData\LocalLow\Raw Lion Workshop\Sailwind\Voyage Recorder` on Windows, or in the same folder inside your CrossOver, Whisky or other compatibility layer's bottle on Mac. Sailwind Chartroom detects them automatically for live tracking
