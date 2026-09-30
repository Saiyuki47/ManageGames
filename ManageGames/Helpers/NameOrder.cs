using System.Globalization;

namespace ManageGames.Helpers;

/// <summary>Sort order of names in the lists.</summary>
public static class NameOrder
{
    // Culture-aware and case-insensitive, so "Ökami" sorts next to "Okami" instead of after "Zelda"
    // (SQLite's NOCASE collation only folds ASCII letters).
    public static readonly StringComparer Comparer = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
}
