using HandCodec.Parser;

namespace HandCodec.Exceptions;

/// <summary>
/// Thrown by <see cref="HandParser.ParseBatch"/> when some segments parse and some fail.
/// Inspect <see cref="SuccessfulSegments"/> to recover the partial result instead of
/// treating the entire batch as failed.
/// </summary>
public sealed class HandBatchPartialException : Exception
{
    public HandBatchPartialException()
        : this(Array.Empty<ParsedHandMessage>(), 0) { }

    public HandBatchPartialException(string message)
        : base(message)
    {
        SuccessfulSegments = Array.Empty<ParsedHandMessage>();
        FailedSegmentCount = 0;
    }

    public HandBatchPartialException(string message, Exception inner)
        : base(message, inner)
    {
        SuccessfulSegments = Array.Empty<ParsedHandMessage>();
        FailedSegmentCount = 0;
    }

    public HandBatchPartialException(
        IReadOnlyList<ParsedHandMessage> successfulSegments,
        int failedSegmentCount)
        : base(
            $"Batch partially parsed: {(successfulSegments ?? throw new ArgumentNullException(nameof(successfulSegments))).Count} succeeded, " +
            $"{failedSegmentCount} failed. Inspect {nameof(SuccessfulSegments)} for recoverable results.")
    {
        SuccessfulSegments = successfulSegments;
        FailedSegmentCount = failedSegmentCount;
    }

    /// <summary>Segments that were successfully parsed.</summary>
    public IReadOnlyList<ParsedHandMessage> SuccessfulSegments { get; }

    /// <summary>Number of segments that failed to parse.</summary>
    public int FailedSegmentCount { get; }
}
