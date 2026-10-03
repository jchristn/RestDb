namespace RestDb.Test.Shared;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;

/// <summary>
/// In-memory telemetry listener for tests: records every measurement from the named meters and every stopped activity
/// from the named activity sources, with no exporter or host involved.
/// </summary>
internal sealed class TelemetryCapture : IDisposable
{
    private readonly MeterListener _MeterListener;
    private readonly ActivityListener _ActivityListener;
    private readonly ConcurrentQueue<CapturedMeasurement> _Measurements = new ConcurrentQueue<CapturedMeasurement>();
    private readonly ConcurrentQueue<Activity> _Activities = new ConcurrentQueue<Activity>();

    public TelemetryCapture(string[] meterNames, string[] activitySourceNames)
    {
        HashSet<string> meters = new HashSet<string>(meterNames ?? Array.Empty<string>(), StringComparer.Ordinal);
        HashSet<string> sources = new HashSet<string>(activitySourceNames ?? Array.Empty<string>(), StringComparer.Ordinal);

        _MeterListener = new MeterListener();
        _MeterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (meters.Contains(instrument.Meter.Name)) listener.EnableMeasurementEvents(instrument);
        };
        _MeterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Record(instrument, value, tags));
        _MeterListener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => Record(instrument, value, tags));
        _MeterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Record(instrument, value, tags));
        _MeterListener.Start();

        _ActivityListener = new ActivityListener
        {
            ShouldListenTo = source => sources.Contains(source.Name),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _Activities.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(_ActivityListener);
    }

    public IReadOnlyList<CapturedMeasurement> Measurements(string instrumentName)
    {
        return _Measurements.Where(m => m.Instrument == instrumentName).ToList();
    }

    public IReadOnlyList<Activity> Spans()
    {
        return _Activities.ToList();
    }

    public void CollectObservables()
    {
        _MeterListener.RecordObservableInstruments();
    }

    public void Dispose()
    {
        _MeterListener.Dispose();
        _ActivityListener.Dispose();
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        Dictionary<string, string?> copy = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> tag in tags) copy[tag.Key] = tag.Value?.ToString();
        _Measurements.Enqueue(new CapturedMeasurement(instrument.Name, instrument.Unit, value, copy));
    }
}
