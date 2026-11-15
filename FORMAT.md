# Voyage Recorder log format

Version 1. Voyage Recorder writes one file per save slot, `slot<N>.jsonl`, in a `Voyage Recorder` folder inside Sailwind's data folder (on Windows, `%USERPROFILE%\AppData\LocalLow\Raw Lion Workshop\Sailwind\Voyage Recorder`).

The file is UTF-8 with one JSON object per line, each ending in `\n`. Numbers always use a decimal point. The file only grows: each reading is appended and the file is closed again, so another program can read it while the game is running. A half-written last line can be seen only mid-write.

Every line has a `type`. Readers should ignore types and fields they don't know, and skip a line that doesn't parse.

## Lines

### header

Written once, when the file is created.

```json
{"type":"header","format":"voyage-recorder","version":1,"mod":"0.1.0","slot":1}
```

`version` changes only when existing fields change meaning. New fields and line types can appear without a version change.

### session

Written before the first reading after each load or new game. A new game in the same slot keeps appending to the same file, so game time can go backwards at a session line. Treat each session as its own track.

```json
{"type":"session","day":12,"hour":7.25,"game":"0a1b2c3d4e5f"}
```

`game` names the game the session belongs to. Voyage Recorder keeps it in the save, so it stays the same across saving and loading and changes with each new game. Two sessions with different `game` values are different games, and the lines after the last such change are the current game. Logs from 0.1.8 and earlier have no `game`; treat their sessions as one earlier game of their own.

### position

```json
{"type":"position","day":12,"hour":7.25,"lat":31.2345678,"lon":-1.2345678,"event":"start","boat":"medi medium","course":214.3,"knots":5.8,"windFrom":71,"windKnots":11.2}
```

| Field | Always | Meaning |
| --- | --- | --- |
| `day` | yes | The game day. |
| `hour` | yes | Hours since midnight, 0 to under 24. `day × 24 + hour` orders readings within a session. |
| `lat`, `lon` | yes | Degrees on Sailwind's globe, north and east positive. |
| `event` | no | `start` or `stop` when recording was switched on or off, `sleep` or `wake` around sleep, `mark` when the player recorded their location with a key (also while not recording). Left out for regular readings. |
| `boat` | no | The type of boat you're aboard, from the game's object name without its number, such as `medi medium` or `junk large`. Left out ashore. |
| `course` | no | The boat's course over ground as a compass bearing, 0 to under 360. Left out when the boat is at rest. |
| `knots` | no | The boat's speed over ground in knots, as the game's speedometer measures it. |
| `windFrom` | no | The compass bearing the wind blows from. |
| `windKnots` | no | The wind's speed in knots. |
| `hull` | no | The boat's hull damage in percent, 0 to 100. Left out ashore. |

Optional fields are also left out when the player switches them off.

A regular reading is skipped when the player is less than 10 metres (about a sixth of a nautical mile on the chart) from the last position written, so standing still adds no lines. Readings with an `event` are always written.

### port

Written the first time in a session that the player stops at a port: ashore, or aboard below 1 knot, within reach of a port the game counts as visited.

```json
{"type":"port","day":12,"hour":9.5,"port":"Gold Rock City","island":1,"lat":30.1234567,"lon":-2.5,"portLat":30.2,"portLon":-2.45,"offsetLat":-1,"offsetLon":2.4}
```

| Field | Meaning |
| --- | --- |
| `port` | The port's name as the game shows it. |
| `island` | The game's island number, from its `island N` scene object, or -1 when none was found. |
| `lat`, `lon` | The island's centre in the game. |
| `portLat`, `portLon` | The port itself. |
| `offsetLat`, `offsetLon` | How far the island was moved from where the game normally places it, in degrees: Scrambled Seas' offset, or 0 without it. A chart can move its own outline of the island by this, as for an imported Scrambled Seas world. Left out when it couldn't be read. |

### mission

Written when a mission list shows, in the player's mission book or on a port's mission board, whether or not recording is on, once a session for each origin and destination, and only when Scrambled Seas is on for the game and gives a destination hint. It holds only what the mission screen shows: the hint is either a heading on Scrambled Seas' compass or coordinates rounded to its precision.

```json
{"type":"mission","day":12,"hour":10,"origin":"Gold Rock City","originIsland":1,"originLat":30.2,"originLon":-2.45,"destination":"Fort Aestrin","destinationIsland":15,"heading":22.5,"points":16,"miles":214}
```

| Field | Always | Meaning |
| --- | --- | --- |
| `origin`, `destination` | yes | Port names as the game shows them. |
| `originIsland`, `destinationIsland` | yes | The game's island numbers, as for `port`. |
| `originLat`, `originLon` | yes | The origin port, where the heading is measured from. |
| `heading`, `points` | no | The direction shown, in degrees, on a compass of `points` directions. |
| `approxLat`, `approxLon`, `decimals` | no | The coordinates shown, rounded to `decimals` places. |
| `miles` | no | The distance shown, in miles, which equal a chart's nautical miles. Left out when Scrambled Seas hides it. |
