// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Aspire.Dashboard.Otlp.Model;

[DebuggerDisplay("{DebuggerToString(),nq}")]
public class OtlpTrace
{
    private OtlpSpan? _rootSpan;

    public ReadOnlyMemory<byte> Key { get; }
    public string TraceId { get; }

    public OtlpSpan FirstSpan => Spans[0]; // There should always be at least one span in a trace.
    public OtlpSpan? RootSpan => _rootSpan;
    public TimeSpan Duration
    {
        get
        {
            var start = FirstSpan.StartTime;
            DateTime end = default;
            foreach (var span in Spans)
            {
                if (span.EndTime > end)
                {
                    end = span.EndTime;
                }
            }
            return end - start;
        }
    }

    public List<OtlpSpan> Spans { get; } = new List<OtlpSpan>();

    public OtlpTraceScope TraceScope { get; }

    public int CalculateDepth(OtlpSpan span)
    {
        var depth = 0;
        var currentSpan = span;
        while (currentSpan != null)
        {
            depth++;
            currentSpan = currentSpan.GetParentSpan();
        }
        return depth;
    }

    public int CalculateMaxDepth() => Spans.Max(CalculateDepth);

    public void AddSpan(OtlpSpan span)
    {
        Spans.Add(span);
        // TODO: Optimize to insert at the right position.
        Spans.Sort(SpanStartDateComparer.Instance);

        if (string.IsNullOrEmpty(span.ParentSpanId))
        {
            _rootSpan = span;
        }
    }

    public OtlpTrace(ReadOnlyMemory<byte> traceId, OtlpTraceScope traceScope)
    {
        Key = traceId;
        TraceId = OtlpHelpers.ToHexString(traceId);
        TraceScope = traceScope;
    }

    public static OtlpTrace Clone(OtlpTrace trace)
    {
        var newTrace = new OtlpTrace(trace.Key, trace.TraceScope);
        foreach (var item in trace.Spans)
        {
            newTrace.AddSpan(OtlpSpan.Clone(item, newTrace));
        }

        return newTrace;
    }

    private string DebuggerToString()
    {
        return $@"TraceId = ""{TraceId}"", Spans = {Spans.Count}, StartTime = {FirstSpan.StartTime.ToLocalTime():h:mm:ss.fff tt}, Duration = {Duration}";
    }

    private sealed class SpanStartDateComparer : IComparer<OtlpSpan>
    {
        public static readonly SpanStartDateComparer Instance = new SpanStartDateComparer();

        public int Compare(OtlpSpan? x, OtlpSpan? y)
        {
            return x!.StartTime.CompareTo(y!.StartTime);
        }
    }
}