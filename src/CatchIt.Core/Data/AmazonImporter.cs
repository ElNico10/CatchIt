using System.Diagnostics;

namespace CatchIt.Core.Data;

/// <summary>What a dataset import did, start to finish.</summary>
/// <param name="Import">What the product import added, skipped and rejected.</param>
/// <param name="RowsRead">Lines read from the file.</param>
/// <param name="RowsSkipped">Lines skipped before import because they were damaged or lacked a title, brand or price.</param>
/// <param name="BrandsTiered">Brands given a quality tier derived from their prices.</param>
/// <param name="AlternativeLinks">New "alternative product" links stored.</param>
/// <param name="Elapsed">Total time, including the tier and alternatives steps.</param>
public record AmazonImportResult(
    ImportSummary Import, long RowsRead, long RowsSkipped, int BrandsTiered, int AlternativeLinks, TimeSpan Elapsed);

/// <summary>
/// Imports an Amazon Reviews 2023 metadata file: reads the products, loads them with simulated
/// listings and price history, then derives brand tiers and alternatives.
/// </summary>
public static class AmazonImporter
{
    /// <summary>Runs the whole import.</summary>
    /// <param name="db">The database to import into.</param>
    /// <param name="path">The <c>meta_*.jsonl.gz</c> file.</param>
    /// <param name="limit">Stop after this many usable products; null imports the whole file.</param>
    /// <param name="importer">The bulk importer to use; defaults to the standard one. Tests pass one with a fixed date.</param>
    public static AmazonImportResult Run(Database db, string path, int? limit = null, BulkImporter? importer = null)
    {
        var watch = Stopwatch.StartNew();
        var reader = new AmazonDatasetReader();

        IEnumerable<ImportedProduct> products = reader.Read(path);
        if (limit != null) products = products.Take(limit.Value);

        var summary = (importer ?? new BulkImporter(db)).Import(products);
        var tiered = CatalogEnricher.DeriveBrandTiers(db);
        var links = CatalogEnricher.BuildAlternatives(db);

        return new AmazonImportResult(summary, reader.RowsRead, reader.RowsSkipped, tiered, links, watch.Elapsed);
    }
}
