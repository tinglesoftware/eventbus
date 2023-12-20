namespace Tingle.EventBus;

/// <summary>Represents the result from scheduling a delayed event.</summary>
/// <param name="id">Scheduling identifier returned by transport.</param>
/// <param name="scheduled">Time at which the event will be availed by the transport.</param>
public readonly struct ScheduledResult(string id, DateTimeOffset scheduled)
{
    /// <summary>Creates and instance of <see cref="ScheduledResult"/>.</summary>
    /// <param name="id">Scheduling identifier returned by transport.</param>
    /// <param name="scheduled">Time at which the event will be availed by the transport.</param>
    public ScheduledResult(long id, DateTimeOffset scheduled) : this(id.ToString(), scheduled) { }

    /// <summary>
    /// Scheduling identifier returned by transport.
    /// </summary>
    public string Id { get; } = id ?? throw new ArgumentNullException(nameof(id));

    /// <summary>
    /// Time at which the event will be availed by the transport.
    /// </summary>
    public DateTimeOffset Scheduled { get; } = scheduled;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ScheduledResult result && Equals(result);

    /// <inheritdoc/>
    public bool Equals(ScheduledResult other) => Id == other.Id && EqualityComparer<DateTimeOffset?>.Default.Equals(Scheduled, other.Scheduled);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id, Scheduled);

    /// <inheritdoc/>
    public override string ToString() => $"{Id} (Availed at {Scheduled:r})";

    /// <inheritdoc/>
    public static bool operator ==(ScheduledResult left, ScheduledResult right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(ScheduledResult left, ScheduledResult right) => !(left == right);

    /// <summary>
    /// Convert <see cref="ScheduledResult"/> to <see cref="string"/>.
    /// </summary>
    /// <param name="result"></param>
    public static implicit operator string(ScheduledResult result) => result.Id;

    /// <summary>
    /// Deconstruct the result into parts
    /// </summary>
    /// <param name="id">See <see cref="Id"/>.</param>
    /// <param name="scheduled">See <see cref="Scheduled"/>.</param>
    public void Deconstruct(out string id, out DateTimeOffset scheduled)
    {
        id = Id;
        scheduled = Scheduled;
    }
}
