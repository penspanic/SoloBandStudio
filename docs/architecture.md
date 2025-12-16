# Architecture

Technical documentation for Solo Band Studio.

## System Overview

```mermaid
flowchart TB
    subgraph VR["VR Layer"]
        XRRig[XR Origin]
        Controllers[Controllers]
    end

    subgraph Instruments["Instruments"]
        IInstrument{{IInstrument}}
        Piano[Piano]
        Drums[Drums]
        Bass[Bass]
        IInstrument --> Piano & Drums & Bass
    end

    subgraph Audio["Audio System"]
        BeatClock[BeatClock]
        LoopStation[LoopStation]
        Mixer[CustomAudioMixer<br/>OnAudioFilterRead]
        SampleCache[(SampleDataCache)]
        BeatClock <--> LoopStation
        LoopStation --> Mixer
        Mixer --> SampleCache
    end

    subgraph UI["UI System"]
        QuickMenu[QuickMenu]
        PianoRoll[PianoRoll]
        LoopStationUI[LoopStation UI]
    end

    subgraph MIDI["MIDI"]
        MidiParser[MidiParser]
        MidiWriter[MidiWriter]
        Songs[(Songs)]
    end

    Controllers --> Instruments
    Instruments --> LoopStation
    LoopStation <--> MidiParser & MidiWriter
    Songs <--> MidiParser & MidiWriter
    LoopStation --> UI
```

## Instrument System

### Interface Design

```mermaid
classDiagram
    class IInstrument {
        <<interface>>
        +InstrumentId
        +InstrumentName
        +Type
        +OnNoteOn
        +OnNoteOff
        +ScheduleNote()
        +StopAllNotes()
    }

    class Keyboard {
        +instrumentType: Piano/Bass
        +KeyboardSynthesizer
    }

    class Drum {
        +DrumKit
        +DrumSoundBank
    }

    IInstrument <|.. Keyboard
    IInstrument <|.. Drum
```

### Keyboard System (Piano/Bass)

Both Piano and Bass use the same `Keyboard` component with different sample banks.

```mermaid
flowchart LR
    KL[KeyboardLayout<br/>88 keys] --> KK[KeyboardKey]
    KK --> KS[KeyboardSynthesizer]
    KS --> SB[(SampleBank)]
```

### Drum System

```mermaid
flowchart LR
    DK[DrumKit] --> DP[DrumPad]
    DT[DrumstickTip] --> DP
    DP --> DSB[(DrumSoundBank)]
```

**DrumPad Types:** Kick, Snare, HiHatClosed, HiHatOpen, TomHigh, TomMid, TomLow, Crash, Ride

## Audio System

```mermaid
flowchart TB
    subgraph Mixer["CustomAudioMixer"]
        AFR[OnAudioFilterRead]
        VP[Voice Pool - 64 voices]
        CQ[ConcurrentQueue]
    end

    subgraph Cache["SampleDataCache"]
        SC[AudioClip → float[]]
    end

    subgraph Components["Components"]
        BC[BeatClock<br/>30-300 BPM]
        MET[Metronome]
        LS[LoopStation<br/>8 tracks]
    end

    CQ --> AFR
    SC --> AFR
    AFR --> VP
    Mixer --> Components
```

**Key Features:**
- Sample-accurate timing via `OnAudioFilterRead`
- Frame-rate independent scheduling
- Thread-safe communication with `ConcurrentQueue`
- Pre-cached audio samples for audio thread access

## UI System

Built with UI Toolkit (UXML/USS).

```mermaid
flowchart TB
    QMC[QuickMenuController]
    QMC --> LSM[LoopStation Tab]
    QMC --> TOD[Time of Day Tab]
    QMC --> MISC[Settings Tab]

    subgraph PianoRoll["PianoRoll"]
        PRV[View] --> Grid & Timeline & NoteManager
    end
```

**UI Panels:**
| Panel | Features |
|-------|----------|
| QuickMenu | Y button toggle, spawns in front of user |
| LoopStation UI | Track controls, BPM, recording |
| PianoRoll UI | Note visualization, drag-and-drop editing |
| ChordQuiz UI | Interactive chord learning |

All panels have grab handles for repositioning in VR.

## MIDI System

```mermaid
flowchart TB
    MFM[MidiFileManager] --> MP[MidiParser] & MW[MidiWriter] & MC[MidiConverter]
    MP & MW --> MF[MidiFile]
    MF --> NE[NoteEvent]
    SA[StreamingAssets/Songs/] -.-> MFM
```

- Standard MIDI file (.mid) support
- Auto-loads files from `StreamingAssets/Songs/`
- Import/export for loop recordings

## XR System

```mermaid
flowchart TB
    XRIT[XR Interaction Toolkit 3.2.2]
    XRIT --> OXR[OpenXR 1.16.0]
    XRIT --> MXR[Meta XR]

    subgraph Custom["Custom Components"]
        TP[TeleportPortal]
        SG[StickyGrabbable]
        RG[ReturnableGrabbable]
    end

    XRIT --> Custom
```

## Rendering

- Universal Render Pipeline (URP)
- Custom `ScreenFadeFeature` for scene transitions
- Day/night cycle system (`TODManager`)

## Feature Checklist

### Instruments
- [x] Piano - 88 keys with velocity
- [x] Drums - Full kit with drumstick collision
- [x] Bass - Keyboard-based with bass samples

### Loop Station
- [x] 8-track recording
- [x] BPM control (30-300)
- [x] Quantization (1/4, 1/8, 1/16)
- [x] Metronome (audio + visual)

### MIDI
- [x] Import/export .mid files
- [x] 10 classical songs (public domain)
- [x] Auto-load from StreamingAssets

### Piano Roll
- [x] Note visualization
- [x] Drag-and-drop editing
- [x] Timeline with playhead
- [x] Zoom/scroll

### VR
- [x] Hand tracking support
- [x] Teleport + continuous movement
- [x] Grabbable UI panels
- [x] Haptic feedback

### Audio
- [x] Sample-accurate DSP scheduling
- [x] 64-voice polyphony
- [x] 3D spatial audio
