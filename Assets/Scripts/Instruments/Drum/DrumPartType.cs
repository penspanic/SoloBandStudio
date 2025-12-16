namespace SoloBandStudio.Instruments.Drum
{
    /// <summary>
    /// Types of drum parts in a drum kit.
    /// Values correspond to General MIDI drum map for compatibility.
    /// </summary>
    public enum DrumPartType
    {
        Kick = 36,
        Snare = 38,
        HiHatClosed = 42,
        HiHatOpen = 46,
        TomHigh = 50,
        TomMid = 47,
        TomLow = 45,
        Crash = 49,
        Ride = 51
    }
}
