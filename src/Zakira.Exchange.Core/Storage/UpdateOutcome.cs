namespace Zakira.Exchange.Core.Storage;

/// <summary>
/// Outcome of <see cref="MemoryStore.Update(Models.MemoryEntry, float[]?, System.DateTimeOffset?)"/>.
/// Distinguishes "row didn't exist" from "row existed but its <c>last_modified_at</c>
/// didn't match the caller's expected value" (optimistic-concurrency failure).
/// </summary>
public enum UpdateOutcome
{
    /// <summary>
    /// The row was updated successfully.
    /// </summary>
    Updated,

    /// <summary>
    /// No row exists for the given (category, key). The caller should
    /// likely fall back to <see cref="MemoryStore.Create(Models.MemoryEntry, float[]?)"/>
    /// or surface a not-found error.
    /// </summary>
    NotFound,

    /// <summary>
    /// A row exists but its <c>last_modified_at</c> does not match the caller's
    /// expected value. The caller's view of the row is stale; they should re-fetch
    /// and re-attempt with the new expected value, or merge and overwrite.
    /// Only returned when the caller passed a non-null <c>expectedLastModifiedAt</c>.
    /// </summary>
    Conflict,
}
