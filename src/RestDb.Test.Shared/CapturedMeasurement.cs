namespace RestDb.Test.Shared;

using System.Collections.Generic;

/// <summary>
/// One measurement captured by <see cref="TelemetryCapture"/>.
/// </summary>
internal sealed class CapturedMeasurement
{
    public CapturedMeasurement(string instrument, string? unit, double value, Dictionary<string, string?> tags)
    {
        Instrument = instrument;
        Unit = unit;
        Value = value;
        Tags = tags;
    }

    public string Instrument { get; }

    public string? Unit { get; }

    public double Value { get; }

    public Dictionary<string, string?> Tags { get; }

    public bool Has(string key, string value)
    {
        return Tags.TryGetValue(key, out string? actual) && actual == value;
    }
}
