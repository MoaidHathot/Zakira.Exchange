namespace Zakira.Exchange.Core.Configuration;

/// <summary>
/// Defines the access mode that controls which operations are available.
/// </summary>
public enum AccessMode
{
    /// <summary>
    /// All operations: create, read, edit, delete.
    /// </summary>
    Full,

    /// <summary>
    /// Read-only: list and search only.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Read + create, but no edit or delete.
    /// </summary>
    AppendOnly,

    /// <summary>
    /// Read + create + edit, but no delete.
    /// </summary>
    NoDelete
}

public static class AccessModeExtensions
{
    public static bool CanCreate(this AccessMode mode) => mode is AccessMode.Full or AccessMode.AppendOnly or AccessMode.NoDelete;
    public static bool CanRead(this AccessMode mode) => true;
    public static bool CanEdit(this AccessMode mode) => mode is AccessMode.Full or AccessMode.NoDelete;
    public static bool CanDelete(this AccessMode mode) => mode is AccessMode.Full;
}
