using Zakira.Exchange.Core.Models;

namespace Zakira.Exchange.Core.Services;

/// <summary>
/// Outcome of <see cref="MemoryService.EditWithConcurrency"/>.
/// </summary>
public enum EditOutcome
{
    /// <summary>
    /// The entry was updated successfully.
    /// </summary>
    Updated,

    /// <summary>
    /// No entry exists for the given (category, key).
    /// </summary>
    NotFound,

    /// <summary>
    /// The entry's current <c>LastModifiedAt</c> does not match the caller's
    /// expected value. The entry's current state is available on
    /// <see cref="EditResult.CurrentLastModifiedAt"/> so callers can retry
    /// with the up-to-date expected value if they choose to.
    /// </summary>
    Conflict,
}

/// <summary>
/// Result of an optimistic-concurrency edit attempt via
/// <see cref="MemoryService.EditWithConcurrency"/>.
/// </summary>
public sealed class EditResult
{
    /// <summary>
    /// The outcome of the attempt.
    /// </summary>
    public required EditOutcome Outcome { get; init; }

    /// <summary>
    /// The updated entry. Present when <see cref="Outcome"/> is
    /// <see cref="EditOutcome.Updated"/>; null otherwise.
    /// </summary>
    public MemoryEntry? Entry { get; init; }

    /// <summary>
    /// The store's current <c>LastModifiedAt</c> for the entry. Present when
    /// <see cref="Outcome"/> is <see cref="EditOutcome.Conflict"/> so the caller
    /// can re-attempt with the correct expected value; null otherwise.
    /// </summary>
    public DateTimeOffset? CurrentLastModifiedAt { get; init; }
}
