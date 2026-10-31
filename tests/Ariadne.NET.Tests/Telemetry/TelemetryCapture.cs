using System.Diagnostics;
using System.Diagnostics.Metrics;
using Ariadne.NET.Telemetry;

namespace Ariadne.NET.Tests.Telemetry;

/// <summary>
/// Listens to the <see cref="AriadneTelemetry.SourceName"/> source and meter for one test. It opens a root span of its
/// own and keeps only the engine spans and measurements in that trace, so tests running in parallel that also resolve
/// packs do not show up here.
/// </summary>
internal sealed class TelemetryCapture : IDisposable
{
    private const string TestSourceName = "Ariadne.NET.Tests.Telemetry";

    // Matched by name in ShouldListenTo: reading this field there could create the source while the listener is still
    // being added, and that source would never get the listener.
    private static readonly ActivitySource TestSource = new(TestSourceName);

    private readonly Lock _gate = new();
    private readonly List<Activity> _spans = [];
    private readonly List<CapturedPoint> _points = [];
    private readonly ActivityListener _activities;
    private readonly MeterListener _meters;
    private readonly Activity _root;
    private readonly ActivityTraceId _traceId;

    public TelemetryCapture()
    {
        _activities = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, TestSourceName, StringComparison.Ordinal) || string.Equals(source.Name, AriadneTelemetry.SourceName, StringComparison.Ordinal),
            Sample = Sample,
            ActivityStopped = Stopped,
        };
        ActivitySource.AddActivityListener(_activities);

        _meters = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (string.Equals(instrument.Meter.Name, AriadneTelemetry.SourceName, StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _meters.SetMeasurementEventCallback<double>(Measured);
        _meters.Start();

        _root = TestSource.StartActivity("telemetry-test")
            ?? throw new InvalidOperationException("The test's root span was not sampled.");
        _traceId = _root.TraceId;
    }

    /// <summary>Gets the stopped engine spans named <paramref name="name"/>, in the test's trace.</summary>
    public Activity[] Spans(string name)
    {
        lock (_gate)
        {
            return [.. _spans.Where(span => string.Equals(span.OperationName, name, StringComparison.Ordinal))];
        }
    }

    /// <summary>Gets the measurements of <paramref name="metric"/> recorded in the test's trace.</summary>
    public CapturedPoint[] Points(string metric)
    {
        lock (_gate)
        {
            return [.. _points.Where(point => string.Equals(point.Metric, metric, StringComparison.Ordinal))];
        }
    }

    public void Dispose()
    {
        _root.Dispose();
        _meters.Dispose();
        _activities.Dispose();
    }

    private ActivitySamplingResult Sample(ref ActivityCreationOptions<ActivityContext> options)
    {
        if (options.Source == TestSource)
        {
            return ActivitySamplingResult.AllDataAndRecorded;
        }

        // An engine span belongs to this test only if it runs under the test's root span. Until the root has started,
        // the trace id is the default one, which no real span has.
        return options.Parent.TraceId == _traceId
            ? ActivitySamplingResult.AllDataAndRecorded
            : ActivitySamplingResult.None;
    }

    private void Stopped(Activity activity)
    {
        if (!string.Equals(activity.Source.Name, AriadneTelemetry.SourceName, StringComparison.Ordinal) || activity.TraceId != _traceId)
        {
            return;
        }

        lock (_gate)
        {
            _spans.Add(activity);
        }
    }

    private void Measured(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        // The proxy records inside the operation's span, so the current span tells whose measurement this is.
        if (Activity.Current?.TraceId != _traceId)
        {
            return;
        }

        var point = new CapturedPoint(instrument.Name, value, tags.ToArray());
        lock (_gate)
        {
            _points.Add(point);
        }
    }
}
