namespace BoulderDash.Core.Audio;

/// <summary>
/// ADSR-artige Lautstärkehüllkurve für SidSynth. Attack/Decay/SustainLevel stammen wo möglich
/// direkt aus der Boulder-Dash-Sound-Dokumentation (Peter Broadribb, elmerproductions.com/sp/
/// peterb/sounds.html); HoldSeconds/ReleaseSeconds sind dort teils nicht angegeben (reine
/// SID-Gate-Steuerung) und wurden nach Gehör kalibriert, um beim Abspielen als PCM-Sample kein
/// hörbares Knacken durch abruptes Abschneiden zu erzeugen — als solche kommentiert.
/// </summary>
public readonly struct Envelope
{
    public required double AttackSeconds { get; init; }
    public required double DecaySeconds { get; init; }

    /// <summary>0..1 (Original: SID-Lautstärke 0-15, hier normiert).</summary>
    public required double SustainLevel { get; init; }

    /// <summary>Haltezeit auf SustainLevel vor dem Release — bei kurzen "Blip"-Effekten ohne
    /// dokumentierte Gate-Dauer kalibriert.</summary>
    public double HoldSeconds { get; init; }

    public double ReleaseSeconds { get; init; }

    public int TotalSamples(int sampleRate) =>
        (int)((AttackSeconds + DecaySeconds + HoldSeconds + ReleaseSeconds) * sampleRate);

    public double AmplitudeAt(int sampleIndex, int sampleRate)
    {
        var t = sampleIndex / (double)sampleRate;

        if (t < AttackSeconds)
        {
            return AttackSeconds <= 0 ? 1.0 : t / AttackSeconds;
        }

        t -= AttackSeconds;
        if (t < DecaySeconds)
        {
            return DecaySeconds <= 0 ? SustainLevel : 1.0 - ((1.0 - SustainLevel) * (t / DecaySeconds));
        }

        t -= DecaySeconds;
        if (t < HoldSeconds)
        {
            return SustainLevel;
        }

        t -= HoldSeconds;
        if (t < ReleaseSeconds)
        {
            return ReleaseSeconds <= 0 ? 0.0 : SustainLevel * (1.0 - (t / ReleaseSeconds));
        }

        return 0.0;
    }
}
