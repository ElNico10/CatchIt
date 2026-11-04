using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace CatchIt.Core.Data;

/// <summary>
/// Reads product rows from an "item metadata" file of the Amazon Reviews 2023 dataset
/// (<c>meta_&lt;Category&gt;.jsonl.gz</c>: one JSON object per line, gzip-compressed) and turns the
/// usable ones into <see cref="ImportedProduct"/> rows for the <see cref="BulkImporter"/>.
/// </summary>
/// <remarks>
/// The file is streamed line by line, so a multi-gigabyte file never has to fit in memory.
/// Only real product facts are taken from it (name, brand, price, rating, categories); the
/// marketplace listings and price history are still simulated by the importer. Rows without a
/// title, a brand (or store) or a positive price are skipped, and so are damaged lines.
/// </remarks>
public class AmazonDatasetReader
{
    /// <summary>Product names are cut to this length; the longest titles are mostly keyword stuffing.</summary>
    public const int MaxNameLength = 200;

    /// <summary>The tier given to every row. Real tiers are derived once all prices are known (see <see cref="CatalogEnricher"/>).</summary>
    public const int PlaceholderTier = 2;

    /// <summary>How many lines have been read so far.</summary>
    public long RowsRead { get; private set; }

    /// <summary>How many lines were skipped because they were damaged or missing a title, brand or price.</summary>
    public long RowsSkipped { get; private set; }

    /// <summary>Streams the usable products out of a dataset file.</summary>
    /// <param name="path">A <c>.jsonl.gz</c> file, or a plain <c>.jsonl</c> file.</param>
    /// <returns>The products, in file order. Enumerated lazily, so the file stays open while it is being read.</returns>
    public IEnumerable<ImportedProduct> Read(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
        using Stream source = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(file, CompressionMode.Decompress)
            : file;
        using var lines = new StreamReader(source);

        while (lines.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            RowsRead++;
            var product = ParseLine(line);
            if (product == null) { RowsSkipped++; continue; }
            yield return product;
        }
    }

    /// <summary>Turns one line of the file into a product.</summary>
    /// <param name="line">One JSON object.</param>
    /// <returns>The product, or <c>null</c> if the line is damaged or lacks a title, brand or positive price.</returns>
    public static ImportedProduct? ParseLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var title = Text(root, "title");
            var price = Price(root);
            var brand = Brand(root);
            if (title == null || brand == null || price is not > 0) return null;

            var categories = Categories(root);
            // The whole path, not just the last name: "Cases" exists under phones, cameras and more, and only
            // products under the same path are comparable. Rows with no categories get none, so they are never compared.
            var category = string.Join(" > ", categories);

            // The brand is added to the keywords by the importer; the category path makes the product findable by type.
            var keywords = string.Join(' ', categories.Prepend(Text(root, "main_category") ?? ""));

            return new ImportedProduct(
                title.Length > MaxNameLength ? title[..MaxNameLength] : title,
                brand, PlaceholderTier, keywords.Trim(), price.Value,
                Rating(root), RatingCount(root), category);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The trimmed string value of a property, or null if it is missing, not text, or blank.</summary>
    private static string? Text(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// The brand: the "Brand" entry of <c>details</c>, falling back to the store name.
    /// </summary>
    private static string? Brand(JsonElement root)
    {
        if (root.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object)
        {
            var brand = Text(details, "Brand");
            if (brand != null) return brand;
        }
        return Text(root, "store");
    }

    /// <summary>The price. The dataset stores it as a number, but some rows hold text such as "$12.99" or "None".</summary>
    private static double? Price(JsonElement root)
    {
        if (!root.TryGetProperty("price", out var value)) return null;

        double price;
        if (value.ValueKind == JsonValueKind.Number)
            price = value.GetDouble();
        else if (value.ValueKind == JsonValueKind.String
                 && double.TryParse(value.GetString()?.Replace("$", "").Replace(",", ""),
                     NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            price = parsed;
        else
            return null;

        return double.IsFinite(price) ? price : null;
    }

    /// <summary>The average rating, or null when it is missing or outside 0 to 5.</summary>
    private static double? Rating(JsonElement root) =>
        root.TryGetProperty("average_rating", out var v) && v.ValueKind == JsonValueKind.Number
        && v.GetDouble() is >= 0 and <= 5 ? v.GetDouble() : null;

    /// <summary>How many ratings the average is based on, or null when missing or negative.</summary>
    private static int? RatingCount(JsonElement root) =>
        root.TryGetProperty("rating_number", out var v) && v.ValueKind == JsonValueKind.Number
        && v.TryGetInt32(out var n) && n >= 0 ? n : null;

    /// <summary>The category path (general to specific), without blanks.</summary>
    private static List<string> Categories(JsonElement root)
    {
        var result = new List<string>();
        if (root.TryGetProperty("categories", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var item in list.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && item.GetString()?.Trim() is { Length: > 0 } c)
                    result.Add(c);
        return result;
    }
}
