# Voyage Recorder log format

Version 1. Voyage Recorder writes one file per save slot, `slot<N>.jsonl`, in a `Voyage Recorder` folder inside Sailwind's data folder (on Windows, `%USERPROFILE%\AppData\LocalLow\Raw Lion Workshop\Sailwind\Voyage Recorder`).

The file is UTF-8 with one JSON object per line, each ending in `\n`. Numbers always use a decimal point. The file only grows: each reading is appended and the file is closed again, so another program can read it while the game is running. A half-written last line can be seen only mid-write.

Every line has a `type`, and from 0.2.0 also has a `utc`: the real time when it was written, stored as Unix seconds with milliseconds. Readers should ignore types and fields they do not recognise, and skip any line that cannot be parsed.

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

From 0.2.0, the session line also describes the world, the loaded mods and Voyage Recorder's keys:

```json
{"type":"session","utc":1794744000.5,"day":12,"hour":7.25,"game":"0a1b2c3d4e5f","world":{"kind":"scrambled","seed":-12345,"islandSpread":10000,"minArchipelagoSeparation":30000,"minIslandSeparation":2000,"lonMin":-12,"lonMax":32,"latMin":26,"latMax":46,"borderExpander":0,"scrambler":60,"scrambledSeas":"6.0.2","layout":"3f2a9c0d11e4b7a8"},"plugins":[{"guid":"com.skeptic043.sailwind.voyagerecorder","name":"Voyage Recorder","version":"0.2.0"}],"keys":{"record":"J","location":"V","marker":"V + LeftShift"}}
```

| Field | Meaning |
| --- | --- |
| `world` | `{"kind":"vanilla"}` when Scrambled Seas is absent or turned off for this game. A Scrambled Seas world contains the settings stored in its save (`seed` and the other fields from the Scrambled Seas save file), `scrambler` (its world format version), `scrambledSeas` (the mod's version), and `layout`, a 16-digit fingerprint of the island positions it produced. Sessions with the same `layout` use the same map. This field is left out when the Scrambled Seas save cannot be read. |
| `plugins` | Every loaded BepInEx plugin, identified by `guid`, `name` and `version` and sorted by `guid`. |
| `keys` | Voyage Recorder's own `record`, `location` and `marker` keys, written as BepInEx stores them in its configuration, such as `J` or `V + LeftShift`. |

### keys

From 0.2.0. Written when the player changes one of Voyage Recorder's keys during a session, using the same `keys` object as the session line.

```json
{"type":"keys","utc":1794744000.5,"keys":{"record":"J","location":"V","marker":"V + LeftShift"}}
```

### position

```json
{"type":"position","day":12,"hour":7.25,"lat":31.2345678,"lon":-1.2345678,"event":"start","boat":"medi medium","course":214.3,"knots":5.8,"windFrom":71,"windKnots":11.2}
```

| Field | Always | Meaning |
| --- | --- | --- |
| `day` | yes | The game day. |
| `hour` | yes | Hours since midnight, 0 to under 24. `day × 24 + hour` orders readings within a session. |
| `lat`, `lon` | yes | Degrees on Sailwind's globe, north and east positive. |
| `event` | no | `start` or `stop` when recording was switched on or off, `sleep` or `wake` around sleep, `mark` when the player recorded their location with a key (also while not recording), and from 0.2.1 `race-start`, `race-checkpoint` or `race-finish` when they chose one from the marker menu (also while not recording). Left out for regular readings. |
| `boat` | no | The type of boat you're aboard, from the game's object name without its number, such as `medi medium` or `junk large`. Left out ashore. |
| `course` | no | The boat's course over ground as a compass bearing, 0 to under 360. Left out when the boat is at rest. |
| `knots` | no | The boat's speed over ground in knots, as the game's speedometer measures it. |
| `windFrom` | no | The compass bearing the wind blows from. |
| `windKnots` | no | The wind's speed in knots. |
| `hull` | no | The boat's hull damage in percent, 0 to 100. Left out ashore. |
| `id` | no | From 0.2.2, 12 hexadecimal digits on a `mark` or race marker, so an `undo` line can name it. |

Optional fields are also left out when the player switches them off.

A regular reading is skipped when the player is less than 10 metres (about a sixth of a nautical mile on the chart) from the last position written, so standing still adds no lines. Readings with an `event` are always written.

### port

Written whenever the player stops at a port while recording: either ashore, or aboard and travelling below 1 knot, within reach of a port the game counts as visited. From 0.2.0, every visit is written. After a `depart` line, the next stop at the same port is a new arrival. Before 0.2.0, only the first visit during a session was written.

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

### depart

From 0.2.0. Written while recording when the player moves more than 1500 metres from the port where they last arrived.

```json
{"type":"depart","utc":1794744000.5,"day":12,"hour":11.25,"port":"Gold Rock City","island":1}
```

`port` and `island` have the same meanings as in a `port` line.

### note

From 0.2.0. Written when the player saves a note from the note box, opened from the marker menu, whether or not recording is on. It contains the same fields as a `position` line, read when the box was opened, plus:

```json
{"type":"note","utc":1794744000.5,"id":"9c1e2f3a4b5d","day":12,"hour":7.25,"lat":31.2345678,"lon":-1.2345678,"boat":"medi medium","course":214.3,"knots":5.8,"text":"Whales off the bow"}
```

| Field | Meaning |
| --- | --- |
| `id` | 12 hexadecimal digits, unique to this note. Readers can use it to recognise a note they already have. |
| `text` | The player's text, up to 20,000 characters, with line breaks preserved. |

A note never has an `event` and is not a `mark`. Cancelling the box writes nothing.

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

### undo

From 0.2.2. Written when the player chooses `Undo` in the marker menu, taking back their latest `mark` or race marker. Readers should drop the position line with that `id`.

```json
{"type":"undo","utc":1794744000.5,"id":"5e8a0c1d2b3f"}
```
