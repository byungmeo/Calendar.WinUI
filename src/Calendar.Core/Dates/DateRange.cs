namespace Calendar.Core.Dates;

/// <summary>A nonempty date range whose end is exclusive.</summary>
public readonly record struct DateRange
{
    /// <summary>Creates a nonempty interval of calendar dates with an exclusive end.</summary>
    public DateRange(DateOnly start, DateOnly endExclusive)
    {
        if (endExclusive <= start)
            throw new ArgumentOutOfRangeException(nameof(endExclusive), "The end must follow the start.");
        Start = start;
        EndExclusive = endExclusive;
    }

    /// <summary>Gets the first included date.</summary>
    public DateOnly Start { get; }
    /// <summary>Gets the first date after the interval.</summary>
    public DateOnly EndExclusive { get; }
    /// <summary>Gets the number of included dates.</summary>
    public int DayCount => EndExclusive.DayNumber - Start.DayNumber;
    /// <summary>Returns whether the date belongs to this interval.</summary>
    public bool Contains(DateOnly date) => date >= Start && date < EndExclusive;
    /// <summary>Returns whether this interval shares any dates with another interval.</summary>
    public bool Overlaps(DateRange other) => Start < other.EndExclusive && EndExclusive > other.Start;
    /// <summary>Returns the shared interval, or null when the intervals do not overlap.</summary>
    public DateRange? Intersect(DateRange other) => Overlaps(other)
        ? new DateRange(Start > other.Start ? Start : other.Start,
            EndExclusive < other.EndExclusive ? EndExclusive : other.EndExclusive)
        : null;
}
