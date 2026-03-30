namespace Zakira.Exchange.Core.Configuration;

/// <summary>
/// Configuration options for Zakira.Exchange.
/// </summary>
public sealed class ZakiraOptions
{
    /// <summary>
    /// Path to the SQLite database file.
    /// Default: ./zakira.db
    /// </summary>
    public string DatabasePath { get; set; } = "zakira.db";

    /// <summary>
    /// Access mode controlling which operations are available.
    /// Default: Full (all operations).
    /// </summary>
    public AccessMode AccessMode { get; set; } = AccessMode.Full;

    /// <summary>
    /// When set, all tools are locked to this category and the category parameter is hidden.
    /// </summary>
    public string? ConstCategory { get; set; }

    /// <summary>
    /// Path to a custom ONNX model file. When null, uses the bundled all-MiniLM-L6-v2 model.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Whether a const-category is configured.
    /// </summary>
    public bool HasConstCategory => !string.IsNullOrWhiteSpace(ConstCategory);
}
