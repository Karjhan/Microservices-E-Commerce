using System.Text.RegularExpressions;

namespace Domain.Commons;

/// <summary>
/// How reference-data names are compared: trimmed, whitespace collapsed, lower-cased. Two names with the same
/// normalized form are the same name (FR-023).
/// </summary>
public static partial class NormalizedName
{
    /// <summary>Trims and collapses runs of whitespace into one space, keeping the case for display.</summary>
    public static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : Whitespace().Replace(value.Trim(), " ");

    /// <summary>The key used for uniqueness: <see cref="Clean"/> then lower-cased.</summary>
    public static string Of(string? value) => Clean(value).ToLowerInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
