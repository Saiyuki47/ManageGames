namespace ManageGames.Helpers
{
    /// <summary>
    /// Shared case- and whitespace-insensitive substring filter used by the list pages
    /// (games, wishlist, categories, companies), so the normalize-and-contains search logic
    /// lives in one place instead of being copy-pasted per controller action.
    /// </summary>
    public static class SearchFilter
    {
        /// <summary>
        /// Returns the items whose selected text contains <paramref name="term"/>, ignoring case
        /// and spaces. An empty/whitespace term matches everything (returns all items).
        /// </summary>
        public static List<T> Filter<T>(IEnumerable<T> items, string? term, Func<T, string> selector)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return items.ToList();
            }

            var normalizedTerm = Normalize(term);
            return items.Where(x => Normalize(selector(x)).Contains(normalizedTerm)).ToList();
        }

        private static string Normalize(string? value)
        {
            return (value ?? string.Empty).ToLower().Replace(" ", string.Empty);
        }
    }
}
