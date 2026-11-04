using System.Text;

namespace CatchIt.Core.Search;

/// <summary>
/// Turns what the user typed into a safe full-text query (user story #5). Every word must match the start of a
/// word in a product's name or keywords, ignoring case, so "wireless head" finds "Apex Wireless Headphones".
/// Typing the middle of a word ("eadphone") does not match; that is the deliberate trade-off for a small, fast index.
/// </summary>
public static class SearchQuery
{
    /// <summary>Most words of a query that are used; extra words are ignored.</summary>
    public const int MaxWords = 8;

    /// <summary>Longest a single word can be before it is cut short.</summary>
    public const int MaxWordLength = 40;

    /// <summary>
    /// Keeps only letters and digits, and turns each word into a quoted prefix match,
    /// e.g. <c>"wireless"* "head"*</c>.
    /// </summary>
    /// <remarks>
    /// The text is never passed to the index as written. The index has its own query language
    /// (AND, OR, NEAR, column filters, quotes), and raw user input could crash a search or
    /// change what it means. Quoting every word makes all of that plain text.
    /// </remarks>
    /// <param name="text">What the user typed.</param>
    /// <returns>The query, or <c>null</c> when the text contains no searchable words.</returns>
    public static string? Build(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var words = new List<string>();
        var word = new StringBuilder();

        void EndWord()
        {
            if (word.Length > 0 && words.Count < MaxWords)
                words.Add(word.Length > MaxWordLength ? word.ToString(0, MaxWordLength) : word.ToString());
            word.Clear();
        }

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch)) word.Append(ch);
            else EndWord();
        }
        EndWord();

        return words.Count == 0 ? null : string.Join(" ", words.Select(w => $"\"{w}\"*"));
    }
}
