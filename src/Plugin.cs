using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace VoyageRecorder
{
    [BepInPlugin(Id, "Voyage Recorder", Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "com.skeptic043.sailwind.voyagerecorder";
        public const string Version = "0.2.2";

        private static Plugin instance;
        private static ManualLogSource log;

        private ConfigEntry<KeyboardShortcut> recordKey;
        private ConfigEntry<KeyboardShortcut> locationKey;
        private ConfigEntry<KeyboardShortcut> markerKey;
        private ConfigEntry<int> interval;
        private ConfigEntry<bool> sleepReadings;
        private ConfigEntry<bool> duringSleep;
        private ConfigEntry<bool> onlyAboard;
        private ConfigEntry<bool> recordBoat;
        private ConfigEntry<bool> recordCourse;
        private ConfigEntry<bool> recordWind;
        private ConfigEntry<bool> recordHull;
        private ConfigEntry<bool> recordPorts;
        private ConfigEntry<bool> recordMissions;
        private ConfigEntry<bool> showDot;
        private ConfigEntry<DotCorner> dotCorner;
        private ConfigEntry<DotSize> dotSize;

        private bool recording;
        // A session line goes before the first reading after each load or new game.
        private bool sessionPending = true;
        private Coroutine timer;
        // Made the first time it is shown.
        private RecordingDot dot;
        private bool writeFailed;
        // The note box while it's open.
        private NoteBox noteBox;
        // The marker menu while it's open.
        private MarkerMenu markerMenu;
        // The latest recorded location or race marker this session, which the marker menu can undo.
        // Where the position before it was written, so undoing it doesn't hold back the next timed reading.
        private (string Id, string Label, Vector3? Before)? lastMark;
        // Where the last position was written this session; a timed reading closer than StillMetres to it is skipped.
        private Vector3? lastWritten;
        // The port the player last arrived at and hasn't left yet. Arriving again needs leaving its reach first.
        private Port atPort;
        // Missions already written this session; Chartroom skips repeats across sessions itself.
        // Missions by origin and destination: a port's board makes new missions each visit, all with the same hint.
        private readonly HashSet<string> writtenMissions = new HashSet<string>();
        private static readonly HashSet<string> logged = new HashSet<string>();

        // The game notes a port as visited within 1000 m of its recovery point. A port counts as reached only
        // while you're that close to it and stopped, ashore or aboard below this speed.
        private const float PortReachMetres = 1500f;
        private const double StoppedKnots = 1.0;
        // Under a sixth of a chart mile, so a skipped reading changes nothing on the chart.
        private const float StillMetres = 10f;

        private void Awake()
        {
            instance = this;
            log = Logger;
            recordKey = Config.Bind("Controls", "Record key", new KeyboardShortcut(KeyCode.J),
                "Press to start or stop recording.");
            locationKey = Config.Bind("Controls", "Location key", new KeyboardShortcut(KeyCode.V),
                "Press to record your location once, whether or not you're recording.");
            markerKey = Config.Bind("Controls", "Marker menu key", new KeyboardShortcut(KeyCode.V, KeyCode.LeftShift),
                "Press to mark a race start, checkpoint or finish, write a note about where you are, or undo your last mark, whether or not you're recording.");
            interval = Config.Bind("Recording", "Interval", 60, new ConfigDescription(
                "Seconds between readings while recording, in game time.",
                new AcceptableValueRange<int>(10, 600)));
            sleepReadings = Config.Bind("Recording", "Sleep and wake readings", true,
                "Take a reading when you fall asleep and when you wake up.");
            duringSleep = Config.Bind("Recording", "Record during sleep", false,
                "Keep taking readings while you sleep.");
            onlyAboard = Config.Bind("Recording", "Only while aboard", false,
                "Skip readings while you are not aboard a boat.");
            recordBoat = Config.Bind("Details", "Boat", true, "Record which boat you are aboard.");
            recordCourse = Config.Bind("Details", "Course and speed", true, "Record the boat's course and speed.");
            recordWind = Config.Bind("Details", "Wind", true, "Record the wind's direction and strength.");
            recordHull = Config.Bind("Details", "Hull damage", true, "Record the hull damage of the boat you're aboard, in percent.");
            recordPorts = Config.Bind("Details", "Port visits", true, "Record each port you stop at.");
            recordMissions = Config.Bind("Details", "Missions", true,
                "Record the missions you see in your mission book or on a port's mission board, with the heading or coordinates Scrambled Seas shows, so Chartroom can draw search areas. Needs Scrambled Seas giving a destination hint.");
            showDot = Config.Bind("Recording dot", "Show", false, new ConfigDescription(
                "Show a flashing red dot in a corner of the screen while recording.", null, new ConfigurationManagerAttributes { Order = 3 }));
            dotCorner = Config.Bind("Recording dot", "Corner", DotCorner.TopRight, new ConfigDescription(
                "The corner the dot sits in.", null, new ConfigurationManagerAttributes { Order = 2 }));
            dotSize = Config.Bind("Recording dot", "Size", DotSize.Small, new ConfigDescription(
                "How big the dot is.", null, new ConfigurationManagerAttributes { Order = 1 }));
            recordKey.SettingChanged += (_, __) => KeysChanged();
            locationKey.SettingChanged += (_, __) => KeysChanged();
            markerKey.SettingChanged += (_, __) => KeysChanged();
            showDot.SettingChanged += (_, __) => UpdateDot();
            dotCorner.SettingChanged += (_, __) => UpdateDot();
            dotSize.SettingChanged += (_, __) => UpdateDot();
            interval.SettingChanged += (_, __) => StartTimer();
            duringSleep.SettingChanged += (_, __) => { if (GameState.sleeping) StartTimer(); };

            new Harmony(Id).PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Voyage Recorder loaded.");
        }

        // The one per-frame cost: a single any-key query. The two keys are checked only after it.
        private void Update()
        {
            if (!Input.anyKeyDown) return;
            bool record = Pressed(recordKey.Value);
            bool location = Pressed(locationKey.Value);
            bool marker = Pressed(markerKey.Value);
            // The marker menu's key is usually the location key with a modifier, which also matches the location key.
            if (marker) location = false;
            // While the note box or marker menu is open, keys are typing or choosing, not commands.
            if ((!record && !location && !marker) || noteBox != null || markerMenu != null || !GameState.playing || GameState.inCursorMenu) return;
            if (record)
            {
                if (recording) StopRecording();
                else StartRecording();
            }
            if (location) RecordLocation();
            if (marker) OpenMarkerMenu();
        }

        private RecorderKeys Keys => new RecorderKeys
        {
            Record = recordKey.Value.ToString(), Location = locationKey.Value.ToString(), Marker = markerKey.Value.ToString()
        };

        // A key changed in Configuration Manager reaches Chartroom's keybinding overlay on the next line it reads.
        // Outside a session, the next session line carries the new keys anyway.
        private void KeysChanged()
        {
            if (sessionPending || !CanRead) return;
            try
            {
                Write(LogFormat.KeysChanged(Keys), GameState.day, Sun.sun.globalTime);
            }
            catch (Exception exception)
            {
                ReadingFailed(exception);
            }
        }

        private static bool Pressed(KeyboardShortcut key)
        {
            if (key.MainKey == KeyCode.None || !Input.GetKeyDown(key.MainKey)) return false;
            foreach (KeyCode modifier in key.Modifiers)
                if (!Input.GetKey(modifier)) return false;
            return true;
        }

        // One position on request, recording or not.
        private void RecordLocation()
        {
            if (!CanRead) return;
            try
            {
                WriteMark("mark", "Location", GameState.currentBoat, PlayerGlobe());
                Notify("Location recorded");
            }
            catch (Exception exception)
            {
                ReadingFailed(exception);
            }
        }

        // A race marker is a recorded location with its own event, written whether or not you're recording.
        private void OpenMarkerMenu()
        {
            if (!CanRead || markerMenu != null) return;
            markerMenu = MarkerMenu.Open(this, lastMark?.Label.ToLowerInvariant(), choice =>
            {
                markerMenu = null;
                if (choice == "note")
                {
                    OpenNote();
                    return;
                }
                if (!CanRead) return;
                try
                {
                    if (choice == "undo")
                    {
                        if (lastMark == null) return;
                        Write(LogFormat.Undo(lastMark.Value.Id), GameState.day, Sun.sun.globalTime);
                        Notify(lastMark.Value.Label + " removed");
                        lastWritten = lastMark.Value.Before;
                        lastMark = null;
                        return;
                    }
                    foreach (var option in MarkerMenu.Choices)
                    {
                        if (option.Event != choice) continue;
                        WriteMark(choice, option.Label, GameState.currentBoat, PlayerGlobe());
                        Notify(option.Label + " recorded");
                    }
                }
                catch (Exception exception)
                {
                    ReadingFailed(exception);
                }
            });
        }

        // The note keeps the reading from when the box opened, the place it's about, and is written on Save.
        private void OpenNote()
        {
            if (!CanRead || noteBox != null) return;
            try
            {
                Reading reading = TakeReading(null, GameState.currentBoat, PlayerGlobe());
                noteBox = NoteBox.Open(this, text => WriteNote(reading, text));
            }
            catch (Exception exception)
            {
                ReadingFailed(exception);
            }
        }

        // False when the note couldn't be written, so the box stays open with the text still in it.
        private bool WriteNote(Reading reading, string text)
        {
            try
            {
                string id = Guid.NewGuid().ToString("N").Substring(0, 12);
                Write(LogFormat.Note(reading, id, text), reading.Day, reading.Hour);
                Notify("Note saved");
                return true;
            }
            catch (Exception exception)
            {
                ReadingFailed(exception);
                return false;
            }
        }

        private void StartRecording()
        {
            // Leaving while recording was off isn't seen; forget that port rather than time its departure now.
            // Still there, it stays the port you're at, so restarting doesn't record a second arrival.
            if (atPort != null && Vector3.Distance(atPort.transform.position, Refs.observerMirror.transform.position) > PortReachMetres) atPort = null;
            recording = true;
            Record("start");
            StartTimer();
            UpdateDot();
            Notify("Recording");
        }

        private void StopRecording()
        {
            Record("stop");
            recording = false;
            StopTimer();
            UpdateDot();
            Notify("Stopped recording");
        }

        // Restarts timed readings when they are wanted: while recording, and while awake unless the player
        // wants them through the night.
        private void StartTimer()
        {
            StopTimer();
            if (!recording || (GameState.sleeping && !duringSleep.Value)) return;
            timer = StartCoroutine(Readings(interval.Value));
        }

        private void StopTimer()
        {
            if (timer != null) StopCoroutine(timer);
            timer = null;
        }

        // Unity schedules the wait itself in scaled time, so no code of ours runs between readings
        // and a paused game (time scale 0) never wakes it.
        private IEnumerator Readings(int seconds)
        {
            var wait = new WaitForSeconds(seconds);
            while (true)
            {
                yield return wait;
                Record(null);
            }
        }

        private void UpdateDot()
        {
            if (recording && showDot.Value) (dot ??= new RecordingDot(this)).Show(dotCorner.Value, dotSize.Value);
            else dot?.Hide();
        }

        private static void Notify(string text)
        {
            if (NotificationUi.instance != null) NotificationUi.instance.ShowNotification(text);
        }

        private static bool CanRead => GameState.playing && !GameState.currentlyLoading;

        // A reading while recording: timed (no event name) or for an event, plus the port check that rides on it.
        private void Record(string eventName)
        {
            if (!recording || !CanRead) return;
            try
            {
                Transform boat = GameState.currentBoat;
                Vector3 globe = PlayerGlobe();
                // Timed readings while standing still add nothing; start, stop, sleep and wake are always written.
                bool still = eventName == null && lastWritten.HasValue && GlobeMetres(lastWritten.Value, globe) < StillMetres;
                if (!still && (boat != null || !onlyAboard.Value)) WritePosition(eventName, boat, globe);
                if (recordPorts.Value) RecordPort(boat);
            }
            catch (Exception exception)
            {
                ReadingFailed(exception);
            }
        }

        private void ReadingFailed(Exception exception)
        {
            if (!writeFailed) log.LogError("Voyage Recorder could not take a reading: " + exception);
            writeFailed = true;
        }

        private static Vector3 PlayerGlobe() => FloatingOriginManager.instance.GetGlobeCoords(Refs.observerMirror.transform);

        // Ground distance between two globe positions: 9000 m to a degree, x east and z north.
        private static float GlobeMetres(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude * 9000f;

        // currentBoat is the boat's model; its parent is the boat itself, with the rigidbody.
        private static Transform BoatRoot(Transform boat) => boat.parent != null ? boat.parent : boat;

        // Speed over ground as the game's speedometer shows it.
        private static double Knots(Vector3 velocity) => new Vector2(velocity.x, velocity.z).magnitude * LogFormat.KnotsPerMetrePerSecond;

        // A recorded location or race marker, with an id so the marker menu can undo the latest one.
        private void WriteMark(string eventName, string label, Transform boat, Vector3 globe)
        {
            Reading reading = TakeReading(eventName, boat, globe);
            reading.Id = Guid.NewGuid().ToString("N").Substring(0, 12);
            Vector3? before = lastWritten;
            Write(LogFormat.Position(reading), reading.Day, reading.Hour);
            lastWritten = globe;
            lastMark = (reading.Id, label, before);
        }

        private void WritePosition(string eventName, Transform boat, Vector3 globe)
        {
            Reading reading = TakeReading(eventName, boat, globe);
            Write(LogFormat.Position(reading), reading.Day, reading.Hour);
            lastWritten = globe;
        }

        private Reading TakeReading(string eventName, Transform boat, Vector3 globe)
        {
            var reading = new Reading
            {
                Day = GameState.day,
                Hour = Sun.sun.globalTime,
                Lat = globe.z,
                Lon = globe.x,
                Event = eventName,
            };
            if (boat != null)
            {
                Transform root = BoatRoot(boat);
                if (recordBoat.Value) reading.Boat = LogFormat.BoatName(root.name);
                Rigidbody body = root.GetComponent<Rigidbody>();
                if (recordCourse.Value && body != null)
                {
                    Vector3 velocity = body.velocity;
                    double knots = Knots(velocity);
                    reading.Knots = knots;
                    // A boat at rest has no course.
                    if (knots >= 0.1) reading.Course = LogFormat.Bearing(velocity.x, velocity.z);
                }
                BoatDamage damage = root.GetComponent<BoatDamage>();
                if (recordHull.Value && damage != null) reading.Hull = Mathf.RoundToInt(damage.hullDamage * 100f);
            }
            if (recordWind.Value)
            {
                // currentWind is the air's velocity, so it points downwind; the wind comes from the opposite way.
                Vector3 wind = Wind.currentWind;
                reading.WindKnots = Knots(wind);
                reading.WindFrom = LogFormat.Bearing(-wind.x, -wind.z);
            }
            return reading;
        }

        // An arrival is written when you stop near a port, and a departure when you then move out of its reach, so
        // every visit is timed, including returns to the same port. The game keeps the last port visited in the
        // save, so a port is only taken while you're near it now.
        private void RecordPort(Transform boat)
        {
            int day = GameState.day;
            double hour = Sun.sun.globalTime;
            if (atPort != null)
            {
                if (Vector3.Distance(atPort.transform.position, Refs.observerMirror.transform.position) <= PortReachMetres) return;
                Write(LogFormat.Depart(new PortDeparture { Day = day, Hour = hour, Port = atPort.GetPortName(), Island = Discoveries.IslandOf(atPort) }), day, hour);
                atPort = null;
            }
            Port port = GameState.lastVisitedPort;
            if (port == null) return;
            Rigidbody body = boat == null ? null : BoatRoot(boat).GetComponent<Rigidbody>();
            if (body != null && Knots(body.velocity) >= StoppedKnots) return;
            if (Vector3.Distance(port.transform.position, Refs.observerMirror.transform.position) > PortReachMetres) return;
            Write(LogFormat.Port(Discoveries.Visit(port, day, hour)), day, hour);
            atPort = port;
        }

        // Only when a mission list shows, in the mission book or on a port's board: each origin and destination
        // once a session, as its mission screen describes it. Recording needn't be on: Chartroom reads the whole
        // log when it starts following.
        private void RecordMissions(Mission[] missions)
        {
            try
            {
                if (!recordMissions.Value || !GameState.playing || missions == null) return;
                int day = GameState.day;
                double hour = Sun.sun.globalTime;
                int written = 0;
                foreach (Mission mission in missions)
                {
                    if (mission?.originPort == null || mission.destinationPort == null) continue;
                    string key = mission.originPort.portIndex + ">" + mission.destinationPort.portIndex;
                    if (writtenMissions.Contains(key)) continue;
                    // Without a hint nothing is written, so a hint turned on later is still recorded.
                    MissionHint? hint = Discoveries.Hint(mission, day, hour);
                    if (!hint.HasValue) continue;
                    Write(LogFormat.Mission(hint.Value), day, hour);
                    writtenMissions.Add(key);
                    written++;
                }
                if (written > 0) log.LogInfo($"Voyage Recorder noted {written} mission{(written == 1 ? "" : "s")}.");
            }
            catch (Exception exception)
            {
                LogOnce("Voyage Recorder could not record missions: " + exception);
            }
        }

        internal static void LogOnce(string message)
        {
            if (logged.Add(message)) log.LogWarning(message);
        }

        private void Write(string line, int day, double hour)
        {
            string folder = Path.Combine(Application.persistentDataPath, "Voyage Recorder");
            string path = Path.Combine(folder, "slot" + SaveSlots.currentSlot + ".jsonl");
            string text = "";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(folder);
                text += LogFormat.Header(Version, SaveSlots.currentSlot);
            }
            if (sessionPending) text += LogFormat.Session(day, hour, GameId, Optional(Discoveries.World, "the world"), Optional(Discoveries.Plugins, "the loaded mods"), Keys);
            text += line;
            // Opened, appended and closed each time, so Chartroom can read while the game writes.
            File.AppendAllText(path, text);
            sessionPending = false;
            writeFailed = false;
        }

        // A session detail that can't be read is left out of the session line, and the line is still written.
        private static T Optional<T>(Func<T> read, string what) where T : class
        {
            try
            {
                return read();
            }
            catch (Exception exception)
            {
                LogOnce($"Voyage Recorder could not read {what}: {exception}");
                return null;
            }
        }

        // Recording is off after every load and new game, and the next reading starts a new session.
        private static void ResetForNewSession()
        {
            if (instance == null) return;
            instance.recording = false;
            instance.StopTimer();
            instance.UpdateDot();
            instance.sessionPending = true;
            instance.lastWritten = null;
            instance.atPort = null;
            if (instance.noteBox != null) instance.noteBox.Close();
            instance.noteBox = null;
            if (instance.markerMenu != null) instance.markerMenu.Close();
            instance.markerMenu = null;
            instance.lastMark = null;
            instance.writtenMissions.Clear();
        }

        // Every mission list goes through DisplayMissions: the mission book's accepted missions, and each page of a
        // port's mission board.
        [HarmonyPatch(typeof(MissionListUI), nameof(MissionListUI.DisplayMissions))]
        private static class MissionListPatch
        {
            private static void Postfix(Mission[] missions)
            {
                if (instance != null) instance.RecordMissions(missions);
            }
        }

        // Which game a session belongs to, kept in the save's mod data so it survives saving and loading. A new
        // game gets a new one; so does a save made before Voyage Recorder kept one, from its next save on.
        private static string GameId => GameState.modData != null && GameState.modData.TryGetValue(Id, out string game) ? game : null;

        private static void NewGameId()
        {
            if (GameState.modData != null) GameState.modData[Id] = Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.LoadGame))]
        private static class LoadGamePatch
        {
            private static void Prefix() => ResetForNewSession();

            // LoadGame puts the save's mod data in place before it returns.
            private static void Postfix()
            {
                if (GameId == null) NewGameId();
            }
        }

        [HarmonyPatch(typeof(StartMenu), "StartNewGame")]
        private static class NewGamePatch
        {
            private static void Prefix()
            {
                ResetForNewSession();
                NewGameId();
            }
        }

        [HarmonyPatch(typeof(Sleep), nameof(Sleep.FallAsleep))]
        private static class FallAsleepPatch
        {
            private static void Postfix()
            {
                if (instance == null || !instance.recording) return;
                if (instance.sleepReadings.Value) instance.Record("sleep");
                if (!instance.duringSleep.Value) instance.StopTimer();
                // Sleep speeds up time, which the dot's flashing is timed by.
                instance.UpdateDot();
            }
        }

        [HarmonyPatch(typeof(Sleep), nameof(Sleep.WakeUp))]
        private static class WakeUpPatch
        {
            private static void Postfix()
            {
                if (instance == null || !instance.recording) return;
                if (instance.sleepReadings.Value) instance.Record("wake");
                if (!instance.duringSleep.Value) instance.StartTimer();
                instance.UpdateDot();
            }
        }
    }
}
