using System;
using System.Collections.Generic;
using UnityEngine;
using SoloBandStudio.Core;
using SoloBandStudio.MIDI;

namespace SoloBandStudio.Audio
{
    /// <summary>
    /// Loop station - manages recording and playback of multiple tracks.
    /// Uses CustomAudioMixer for sample-accurate DSP scheduling.
    /// </summary>
    public class LoopStation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BeatClock beatClock;

        [Header("Settings")]
        [SerializeField] private int maxTracks = 8;
        [SerializeField] private double lookaheadSeconds = 1.0; // How far ahead to schedule (in seconds, BPM-independent)

        [Header("Quantize")]
        [SerializeField] private bool autoQuantize = true;
        [SerializeField] private float quantizeValue = 0.25f; // 1/16 note

        [Header("State (Debug)")]
        [SerializeField] private bool isPlaying;
        [SerializeField] private bool isRecording;
        [SerializeField] private int trackCount;
        [SerializeField] private int soloTrackIndex = -1;

        // Track data
        private List<LoopTrackData> tracks = new List<LoopTrackData>();

        // Recording state
        private IInstrument recordingInstrument;
        private LoopTrackData recordingTrack;
        private Dictionary<int, float> activeNotes = new Dictionary<int, float>();

        // Playback scheduling state
        private int lastScheduledLoop = -1;
        private double lastScheduledBeat = -1;
        private List<ScheduledEvent> scheduledEvents = new List<ScheduledEvent>();

        // Registered instruments (by ID)
        private Dictionary<string, IInstrument> instruments = new Dictionary<string, IInstrument>();

        // Reference to audio mixer (for cleanup)
        private CustomAudioMixer audioMixer;

        // Events
        public event Action OnPlayStarted;
        public event Action OnPlayStopped;
        public event Action<LoopTrackData> OnTrackCreated;
        public event Action<LoopTrackData> OnTrackRemoved;
        public event Action<LoopTrackData> OnRecordingStarted;
        public event Action<LoopTrackData> OnRecordingStopped;

        // Properties
        public bool IsPlaying => isPlaying;
        public bool IsRecording => isRecording;
        public int TrackCount => tracks.Count;
        public int MaxTracks => maxTracks;
        public int SoloTrackIndex => soloTrackIndex;
        public BeatClock BeatClock => beatClock;
        public IReadOnlyList<LoopTrackData> Tracks => tracks.AsReadOnly();
        public bool AutoQuantize
        {
            get => autoQuantize;
            set => autoQuantize = value;
        }
        public float QuantizeValue
        {
            get => quantizeValue;
            set => quantizeValue = Mathf.Max(0.03125f, value); // Min 1/128 note
        }

        /// <summary>
        /// Represents a scheduled note event with its handle.
        /// </summary>
        private class ScheduledEvent
        {
            public int TrackIndex;
            public int EventIndex;
            public int LoopNumber;
            public ScheduledNoteHandle Handle;
            public double NoteOffDspTime;
        }

        private void Start()
        {
            audioMixer = CustomAudioMixer.Instance;
        }

        private void OnEnable()
        {
            if (beatClock != null)
            {
                beatClock.OnPlayStateChanged += HandleBeatClockStateChanged;
                beatClock.OnLoopComplete += HandleLoopComplete;
            }
        }

        private void OnDisable()
        {
            if (beatClock != null)
            {
                beatClock.OnPlayStateChanged -= HandleBeatClockStateChanged;
                beatClock.OnLoopComplete -= HandleLoopComplete;
            }

            if (isRecording) StopRecording();
        }

        private void Update()
        {
            // Don't schedule during count-in - wait for actual playback
            if (!isPlaying || beatClock == null || !beatClock.IsPlaying || beatClock.IsCountingIn) return;

            ScheduleUpcomingEvents();
            ProcessScheduledNoteOffs();
        }

        #region Scheduling

        /// <summary>
        /// Schedule events that will play within the lookahead window.
        /// </summary>
        private void ScheduleUpcomingEvents()
        {
            int currentLoop = beatClock.CurrentLoop;
            double currentBeat = beatClock.CurrentBeatPositionPrecise;
            int totalBeats = beatClock.TotalBeats;

            // Reset scheduling state on new loop
            if (currentLoop > lastScheduledLoop)
            {
                lastScheduledLoop = currentLoop;
                lastScheduledBeat = -0.001; // Start fresh for new loop
            }

            // Calculate scheduling window (convert seconds to beats for BPM-independent lookahead)
            double lookaheadBeats = beatClock.SecondsToBeats(lookaheadSeconds);
            double scheduleFrom = lastScheduledBeat;
            double scheduleUpTo = currentBeat + lookaheadBeats;

            // Don't schedule beyond current loop + 1
            if (scheduleUpTo > totalBeats)
            {
                scheduleUpTo = totalBeats;
            }

            // Schedule events in window
            for (int trackIdx = 0; trackIdx < tracks.Count; trackIdx++)
            {
                var track = tracks[trackIdx];
                if (track.IsMuted) continue;
                if (soloTrackIndex >= 0 && trackIdx != soloTrackIndex) continue;

                if (!instruments.TryGetValue(track.InstrumentId, out var instrument)) continue;

                for (int evtIdx = 0; evtIdx < track.Events.Count; evtIdx++)
                {
                    var evt = track.Events[evtIdx];

                    // Check if in scheduling window
                    if (evt.beatTime <= scheduleFrom || evt.beatTime > scheduleUpTo) continue;

                    // Schedule this event
                    ScheduleNoteEvent(instrument, track, trackIdx, evtIdx, evt, currentLoop);
                }
            }

            lastScheduledBeat = scheduleUpTo;

            // If we've scheduled up to the end of the loop, also schedule start of next loop
            if (scheduleUpTo >= totalBeats - 0.001)
            {
                ScheduleNextLoopStart(currentLoop + 1);
            }
        }

        /// <summary>
        /// Schedule events at the start of the next loop.
        /// </summary>
        private void ScheduleNextLoopStart(int nextLoop)
        {
            double lookaheadBeats = beatClock.SecondsToBeats(lookaheadSeconds);
            double scheduleUpTo = (beatClock.CurrentBeatPositionPrecise + lookaheadBeats) - beatClock.TotalBeats;
            if (scheduleUpTo < 0) return;

            for (int trackIdx = 0; trackIdx < tracks.Count; trackIdx++)
            {
                var track = tracks[trackIdx];
                if (track.IsMuted) continue;
                if (soloTrackIndex >= 0 && trackIdx != soloTrackIndex) continue;

                if (!instruments.TryGetValue(track.InstrumentId, out var instrument)) continue;

                for (int evtIdx = 0; evtIdx < track.Events.Count; evtIdx++)
                {
                    var evt = track.Events[evtIdx];

                    // Only schedule events in the early part of the loop
                    if (evt.beatTime > scheduleUpTo) continue;

                    // Check if already scheduled for this loop
                    if (IsEventScheduled(trackIdx, evtIdx, nextLoop)) continue;

                    // Schedule for next loop
                    ScheduleNoteEvent(instrument, track, trackIdx, evtIdx, evt, nextLoop);
                }
            }
        }

        private bool IsEventScheduled(int trackIdx, int evtIdx, int loop)
        {
            foreach (var se in scheduledEvents)
            {
                if (se.TrackIndex == trackIdx && se.EventIndex == evtIdx && se.LoopNumber == loop)
                    return true;
            }
            return false;
        }

        private void ScheduleNoteEvent(IInstrument instrument, LoopTrackData track, int trackIdx, int evtIdx, NoteEvent evt, int loopNumber)
        {
            // Calculate DSP times
            double noteOnDsp = beatClock.GetDspTimeForBeatInLoop(evt.beatTime, loopNumber);
            double noteOffDsp = noteOnDsp + beatClock.BeatsToSeconds(evt.duration > 0 ? evt.duration : 0.25);

            double currentDsp = AudioSettings.dspTime;

            // If note is slightly in the past (due to frame drop), play immediately instead of skipping
            // This prevents notes from being dropped during frame hiccups
            const double lateToleranceSeconds = 0.15; // Allow up to 150ms late notes to still play
            if (noteOnDsp < currentDsp - lateToleranceSeconds)
            {
                // Too far in the past, skip this note
                return;
            }

            // If note should have played but we're within tolerance, play it now
            if (noteOnDsp < currentDsp)
            {
                noteOnDsp = currentDsp + 0.005; // Play 5ms from now
                noteOffDsp = noteOnDsp + beatClock.BeatsToSeconds(evt.duration > 0 ? evt.duration : 0.25);
            }

            // Schedule the note
            float velocity = evt.velocity * track.Volume;
            var handle = instrument.ScheduleNote(evt.note, velocity, noteOnDsp);

            if (handle != null)
            {
                // Track for note-off scheduling
                scheduledEvents.Add(new ScheduledEvent
                {
                    TrackIndex = trackIdx,
                    EventIndex = evtIdx,
                    LoopNumber = loopNumber,
                    Handle = handle,
                    NoteOffDspTime = noteOffDsp
                });
            }
        }

        /// <summary>
        /// Process scheduled note-offs as their time approaches.
        /// </summary>
        private void ProcessScheduledNoteOffs()
        {
            double currentDsp = AudioSettings.dspTime;
            double scheduleAhead = 0.3; // Schedule note-offs 300ms ahead for safety

            for (int i = scheduledEvents.Count - 1; i >= 0; i--)
            {
                var se = scheduledEvents[i];

                // Clean up events with invalid handles first
                if (se.Handle == null || !se.Handle.IsValid)
                {
                    scheduledEvents.RemoveAt(i);
                    continue;
                }

                // Check if it's time to schedule the note-off (schedule ahead of time)
                double timeUntilNoteOff = se.NoteOffDspTime - currentDsp;
                if (timeUntilNoteOff < scheduleAhead && timeUntilNoteOff > -0.1)
                {
                    var instrument = GetInstrumentForTrack(se.TrackIndex);
                    instrument?.ScheduleNoteOff(se.Handle, se.NoteOffDspTime, 0.05f);
                    scheduledEvents.RemoveAt(i);
                }
                // Clean up past events that were missed
                else if (timeUntilNoteOff <= -0.1)
                {
                    // Force stop immediately if we missed the note-off
                    var instrument = GetInstrumentForTrack(se.TrackIndex);
                    if (instrument != null && se.Handle.IsValid)
                    {
                        instrument.ScheduleNoteOff(se.Handle, currentDsp + 0.01, 0.02f);
                    }
                    scheduledEvents.RemoveAt(i);
                }
            }
        }

        private IInstrument GetInstrumentForTrack(int trackIdx)
        {
            if (trackIdx < 0 || trackIdx >= tracks.Count) return null;
            instruments.TryGetValue(tracks[trackIdx].InstrumentId, out var instrument);
            return instrument;
        }

        private void HandleLoopComplete()
        {
            // Clean up any stale scheduled events from previous loops
            int currentLoop = beatClock.CurrentLoop;
            scheduledEvents.RemoveAll(se => se.LoopNumber < currentLoop - 1);
        }

        #endregion

        #region Instrument Registration

        public void RegisterInstrument(IInstrument instrument)
        {
            if (instrument == null) return;
            instruments[instrument.InstrumentId] = instrument;
            Debug.Log($"[LoopStation] Registered: {instrument.InstrumentName} (ID: {instrument.InstrumentId})");
        }

        public void UnregisterInstrument(IInstrument instrument)
        {
            if (instrument == null) return;
            instruments.Remove(instrument.InstrumentId);
            Debug.Log($"[LoopStation] Unregistered: {instrument.InstrumentName}");
        }

        public IInstrument GetInstrument(string instrumentId)
        {
            instruments.TryGetValue(instrumentId, out var instrument);
            return instrument;
        }

        public IInstrument GetInstrumentByType(InstrumentType type)
        {
            foreach (var kvp in instruments)
            {
                if (kvp.Value.Type == type) return kvp.Value;
            }
            return null;
        }

        #endregion

        #region Playback Control

        public void Play()
        {
            // Don't start if already playing or counting in
            if (isPlaying) return;
            if (beatClock != null && beatClock.IsCountingIn) return;

            isPlaying = true;
            lastScheduledLoop = -1;
            lastScheduledBeat = -0.001;
            scheduledEvents.Clear();

            if (beatClock != null && !beatClock.IsPlaying)
            {
                beatClock.Play();
            }

            OnPlayStarted?.Invoke();
            Debug.Log("[LoopStation] Playback started");
        }

        public void Stop()
        {
            if (!isPlaying && !isRecording) return;

            if (isRecording) StopRecording();

            isPlaying = false;

            // Cancel all scheduled notes
            foreach (var se in scheduledEvents)
            {
                if (se.Handle != null && se.Handle.IsValid)
                {
                    var instrument = GetInstrumentForTrack(se.TrackIndex);
                    instrument?.StopAllNotes();
                }
            }
            scheduledEvents.Clear();

            // Stop all instruments
            foreach (var kvp in instruments)
            {
                kvp.Value.StopAllNotes();
            }

            if (beatClock != null && beatClock.IsPlaying)
            {
                beatClock.Stop();
            }

            // Stop all sounds in the mixer
            audioMixer?.StopAllNotes();

            OnPlayStopped?.Invoke();
            Debug.Log("[LoopStation] Playback stopped");
        }

        public void TogglePlay()
        {
            // If playing or counting in, stop
            if (isPlaying || (beatClock != null && beatClock.IsCountingIn))
                Stop();
            else
                Play();
        }

        private void HandleBeatClockStateChanged()
        {
            if (beatClock != null && !beatClock.IsPlaying && isPlaying)
            {
                isPlaying = false;
                OnPlayStopped?.Invoke();
            }
        }

        #endregion

        #region Recording

        public void StartRecording(IInstrument instrument)
        {
            if (isRecording || instrument == null || tracks.Count >= maxTracks) return;

            recordingTrack = new LoopTrackData(instrument.InstrumentName, instrument.InstrumentId, instrument.Type);
            recordingInstrument = instrument;
            activeNotes.Clear();

            instrument.OnNoteOn += HandleNoteOn;
            instrument.OnNoteOff += HandleNoteOff;

            isRecording = true;

            if (!isPlaying) Play();

            OnRecordingStarted?.Invoke(recordingTrack);
            Debug.Log($"[LoopStation] Recording: {instrument.InstrumentName}");
        }

        public void StopRecording()
        {
            if (!isRecording || recordingTrack == null) return;

            if (recordingInstrument != null)
            {
                recordingInstrument.OnNoteOn -= HandleNoteOn;
                recordingInstrument.OnNoteOff -= HandleNoteOff;
            }

            // Finalize any held notes
            float currentBeat = beatClock?.CurrentBeatPosition ?? 0f;
            foreach (var kvp in activeNotes)
            {
                float duration = currentBeat - kvp.Value;
                if (duration < 0) duration += beatClock.TotalBeats;
                recordingTrack.SetNoteDuration(kvp.Key, duration);
            }
            activeNotes.Clear();

            recordingTrack.SortEvents();

            if (recordingTrack.EventCount > 0)
            {
                tracks.Add(recordingTrack);
                trackCount = tracks.Count;
                OnTrackCreated?.Invoke(recordingTrack);
            }

            var finishedTrack = recordingTrack;
            recordingTrack = null;
            recordingInstrument = null;
            isRecording = false;

            OnRecordingStopped?.Invoke(finishedTrack);
        }

        public void ToggleRecording(IInstrument instrument)
        {
            if (isRecording) StopRecording();
            else StartRecording(instrument);
        }

        private void HandleNoteOn(int midiNote, float velocity)
        {
            if (!isRecording || recordingTrack == null || beatClock == null) return;

            float beatTime = beatClock.CurrentBeatPosition;
            if (autoQuantize)
                beatTime = Mathf.Round(beatTime / quantizeValue) * quantizeValue;

            recordingTrack.AddEvent(new NoteEvent(beatTime, midiNote, velocity, 0f));
            activeNotes[midiNote] = beatTime;
        }

        private void HandleNoteOff(int midiNote)
        {
            if (!isRecording || recordingTrack == null || beatClock == null) return;

            if (activeNotes.TryGetValue(midiNote, out float startBeat))
            {
                float currentBeat = beatClock.CurrentBeatPosition;
                if (autoQuantize)
                    currentBeat = Mathf.Round(currentBeat / quantizeValue) * quantizeValue;

                float duration = currentBeat - startBeat;
                if (duration < 0) duration += beatClock.TotalBeats;
                // Ensure minimum duration of one quantize unit
                if (autoQuantize && duration < quantizeValue)
                    duration = quantizeValue;

                recordingTrack.SetNoteDuration(midiNote, duration);
                activeNotes.Remove(midiNote);
            }
        }

        #endregion

        #region Track Management

        public bool RemoveTrack(int index)
        {
            if (index < 0 || index >= tracks.Count) return false;

            var track = tracks[index];
            tracks.RemoveAt(index);
            trackCount = tracks.Count;

            if (soloTrackIndex == index) soloTrackIndex = -1;
            else if (soloTrackIndex > index) soloTrackIndex--;

            OnTrackRemoved?.Invoke(track);
            return true;
        }

        public void ClearAllTracks()
        {
            if (isRecording) StopRecording();

            foreach (var track in tracks)
                OnTrackRemoved?.Invoke(track);

            tracks.Clear();
            trackCount = 0;
            soloTrackIndex = -1;
        }

        public void SetSolo(int index)
        {
            soloTrackIndex = (index >= 0 && index < tracks.Count) ? index : -1;
        }

        public void ToggleSolo(int index)
        {
            SetSolo(soloTrackIndex == index ? -1 : index);
        }

        public void ToggleMute(int index)
        {
            if (index >= 0 && index < tracks.Count)
                tracks[index].IsMuted = !tracks[index].IsMuted;
        }

        public LoopTrackData GetTrack(int index)
        {
            return (index >= 0 && index < tracks.Count) ? tracks[index] : null;
        }

        #endregion

        #region Preset & MIDI

        public void LoadPreset(SongPreset preset, bool autoPlay = false)
        {
            if (preset == null) return;

            if (isPlaying || isRecording) Stop();

            ClearAllTracks();

            if (beatClock != null)
            {
                beatClock.BPM = preset.BPM;
                beatClock.BeatsPerBar = preset.BeatsPerBar;
                beatClock.TotalBars = preset.TotalBars;
            }

            foreach (var track in preset.CloneTracks())
            {
                tracks.Add(track);
                OnTrackCreated?.Invoke(track);
            }
            trackCount = tracks.Count;

            Debug.Log($"[LoopStation] Loaded: {preset.SongName} ({tracks.Count} tracks)");

            if (autoPlay) Play();
        }

        public SongPreset ExportToPreset(string songName, string artist = "User")
        {
            var preset = ScriptableObject.CreateInstance<SongPreset>();
            preset.SetMetadata(songName, artist, "Exported from LoopStation",
                beatClock?.BPM ?? 120f, beatClock?.BeatsPerBar ?? 4, beatClock?.TotalBars ?? 4);

            foreach (var track in tracks)
                preset.AddTrack(track.Clone());

            return preset;
        }

        public bool LoadFromMidi(string filename, bool autoPlay = false)
        {
            var (loadedTracks, metadata) = MidiFileManager.Instance.LoadTracksWithMetadata(filename);
            return LoadTracksInternal(loadedTracks, metadata, autoPlay);
        }

        public bool LoadFromStreamingAssets(string filename, bool autoPlay = false)
        {
            var (loadedTracks, metadata) = MidiFileManager.Instance.LoadFromStreamingAssetsWithMetadata(filename);
            return LoadTracksInternal(loadedTracks, metadata, autoPlay);
        }

        public bool LoadFromMidiPath(string fullPath, bool autoPlay = false)
        {
            var (loadedTracks, metadata) = MidiFileManager.Instance.LoadFromPathWithMetadata(fullPath);
            return LoadTracksInternal(loadedTracks, metadata, autoPlay);
        }

        private bool LoadTracksInternal(List<LoopTrackData> loadedTracks, SongMetadata metadata, bool autoPlay)
        {
            if (loadedTracks == null || loadedTracks.Count == 0) return false;

            if (isPlaying || isRecording) Stop();
            ClearAllTracks();

            if (beatClock != null)
            {
                beatClock.ApplyMetadata(metadata);
            }

            foreach (var track in loadedTracks)
            {
                tracks.Add(track);
                OnTrackCreated?.Invoke(track);
            }
            trackCount = tracks.Count;

            Debug.Log($"[LoopStation] Loaded song: {metadata}");

            if (autoPlay) Play();
            return true;
        }

        public void SaveToMidi(string filename, string songName = null)
        {
            if (tracks.Count == 0) return;
            MidiFileManager.Instance.SaveTracks(filename, new List<LoopTrackData>(tracks),
                beatClock?.BPM ?? 120f, beatClock?.BeatsPerBar ?? 4, songName ?? filename);
        }

        public string[] GetSavedMidiFiles() => MidiFileManager.Instance.GetSavedFiles();
        public bool DeleteMidiFile(string filename) => MidiFileManager.Instance.Delete(filename);
        public void OpenMidiSaveFolder() => MidiFileManager.Instance.OpenSaveFolder();

        public bool IsPresetCompatible(SongPreset preset)
        {
            if (preset == null) return false;
            foreach (var track in preset.Tracks)
            {
                // Check if we have an instrument that can play this track
                // First try by ID, then by Type
                if (!string.IsNullOrEmpty(track.InstrumentId) && instruments.ContainsKey(track.InstrumentId))
                    continue;
                if (GetInstrumentByType(track.InstrumentType) != null)
                    continue;
                return false;
            }
            return true;
        }

        public List<InstrumentType> GetMissingInstruments(SongPreset preset)
        {
            var missing = new List<InstrumentType>();
            if (preset == null) return missing;

            var checkedTypes = new HashSet<InstrumentType>();
            foreach (var track in preset.Tracks)
            {
                if (!checkedTypes.Contains(track.InstrumentType))
                {
                    checkedTypes.Add(track.InstrumentType);
                    // Check if we have an instrument of this type
                    if (GetInstrumentByType(track.InstrumentType) == null)
                        missing.Add(track.InstrumentType);
                }
            }
            return missing;
        }

        public bool HasContent()
        {
            foreach (var track in tracks)
                if (track.EventCount > 0) return true;
            return false;
        }

        public int GetTotalEventCount()
        {
            int total = 0;
            foreach (var track in tracks)
                total += track.EventCount;
            return total;
        }

        #endregion
    }
}
