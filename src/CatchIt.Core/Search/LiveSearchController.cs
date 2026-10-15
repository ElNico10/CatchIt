namespace CatchIt.Core.Search;

/// <summary>Results delivered by a <see cref="LiveSearchController{TPage}"/>.</summary>
/// <typeparam name="TPage">The kind of result page the searches return.</typeparam>
/// <param name="Text">The text the results are for.</param>
/// <param name="Page">The results.</param>
/// <param name="IsFinal">
/// False for quick as-you-type results; true for the full search started by
/// <see cref="LiveSearchController{TPage}.Submit"/>.
/// </param>
public record LiveSearchResult<TPage>(string Text, TPage Page, bool IsFinal);

/// <summary>
/// Drives search-as-you-type without freezing the screen. Each keystroke waits a moment (so fast
/// typing does one search, not one per letter), runs the search on a background thread, and
/// delivers the result to the screen. A newer request always cancels the older one, so an old,
/// slow search can never overwrite the results for what the user has typed since.
/// </summary>
/// <remarks>
/// Create it on the screen's UI thread, after the screen's controls exist. Events are then raised on
/// that thread, so a handler can update controls directly. Created elsewhere (no synchronization
/// context, as in tests), events are raised on a background thread.
/// <para>
/// Typical use: call <see cref="TextChanged"/> from the search box's TextChanged event, and
/// <see cref="Submit"/> when the user presses Enter or the Search button.
/// </para>
/// </remarks>
/// <typeparam name="TPage">The kind of result page the searches return.</typeparam>
public sealed class LiveSearchController<TPage> : IDisposable
{
    /// <summary>How long to wait after the last keystroke before searching.</summary>
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(150);

    private readonly Func<string?, CancellationToken, Task<TPage>> _quick;
    private readonly Func<string?, CancellationToken, Task<TPage>> _full;
    private readonly TimeSpan _debounce;
    private readonly SynchronizationContext? _context;
    private readonly object _gate = new();
    private CancellationTokenSource? _current;
    private bool _disposed;

    /// <summary>Raised with the results of the newest request. Never raised for a superseded request.</summary>
    public event EventHandler<LiveSearchResult<TPage>>? ResultsReady;

    /// <summary>Raised when the newest request failed (for example the database could not be read).</summary>
    public event EventHandler<Exception>? SearchFailed;

    /// <summary>Creates a controller from two search functions. Most callers use <see cref="LiveSearchController.For(ProductSearchService, int, TimeSpan?)"/>.</summary>
    /// <param name="quick">The fast search used while typing.</param>
    /// <param name="full">The full search used on Submit.</param>
    /// <param name="debounce">How long to wait after the last keystroke. Defaults to <see cref="DefaultDebounce"/>.</param>
    public LiveSearchController(
        Func<string?, CancellationToken, Task<TPage>> quick,
        Func<string?, CancellationToken, Task<TPage>> full,
        TimeSpan? debounce = null)
    {
        _quick = quick;
        _full = full;
        _debounce = debounce ?? DefaultDebounce;
        _context = SynchronizationContext.Current;
    }

    /// <summary>
    /// The search text changed. After the debounce delay, runs a quick search and raises
    /// <see cref="ResultsReady"/> with <c>IsFinal = false</c>. Cancels any earlier request.
    /// </summary>
    /// <param name="text">The current contents of the search box.</param>
    public void TextChanged(string? text) => Start(async token =>
    {
        await Task.Delay(_debounce, token).ConfigureAwait(false);
        var page = await _quick(text, token).ConfigureAwait(false);
        return new LiveSearchResult<TPage>(text ?? string.Empty, page, IsFinal: false);
    });

    /// <summary>
    /// The user asked for the real results (pressed Enter or Search). Searches immediately, with no
    /// delay, using the full search, and raises <see cref="ResultsReady"/> with <c>IsFinal = true</c>.
    /// Cancels any earlier request.
    /// </summary>
    /// <param name="text">The current contents of the search box.</param>
    public void Submit(string? text) => Start(async token =>
    {
        var page = await _full(text, token).ConfigureAwait(false);
        return new LiveSearchResult<TPage>(text ?? string.Empty, page, IsFinal: true);
    });

    /// <summary>Cancels whatever is pending, for example when the screen closes. No event is raised.</summary>
    public void Cancel()
    {
        lock (_gate)
        {
            _current?.Cancel();
        }
    }

    /// <summary>Cancels pending work and stops raising events.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _current?.Cancel();
        }
    }

    /// <summary>Starts a request, cancelling the previous one first.</summary>
    private void Start(Func<CancellationToken, Task<LiveSearchResult<TPage>>> work)
    {
        CancellationTokenSource source;
        lock (_gate)
        {
            if (_disposed) return;
            _current?.Cancel(); // the older request must never deliver results
            source = _current = new CancellationTokenSource();
        }
        var token = source.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await work(token).ConfigureAwait(false);
                Deliver(token, () => ResultsReady?.Invoke(this, result));
            }
            catch (OperationCanceledException)
            {
                // Superseded or cancelled: nothing to report.
            }
            catch (Exception ex)
            {
                Deliver(token, () => SearchFailed?.Invoke(this, ex));
            }
        });
    }

    /// <summary>
    /// Raises an event on the screen's thread, unless this request has been superseded in the
    /// meantime (checked again on that thread, since a newer request may have started while posting).
    /// </summary>
    private void Deliver(CancellationToken token, Action raise)
    {
        void Run()
        {
            if (token.IsCancellationRequested) return;
            raise();
        }

        if (token.IsCancellationRequested) return;
        if (_context != null) _context.Post(_ => Run(), null);
        else Run();
    }
}

/// <summary>Ready-made <see cref="LiveSearchController{TPage}"/> setups for the app's search services.</summary>
public static class LiveSearchController
{
    /// <summary>A controller for product-level results.</summary>
    /// <param name="search">The service to search with.</param>
    /// <param name="limit">How many hits each search returns at most.</param>
    /// <param name="debounce">How long to wait after the last keystroke.</param>
    public static LiveSearchController<SearchPage> For(ProductSearchService search, int limit = 50, TimeSpan? debounce = null) =>
        new(
            (text, token) => search.QuickSearchAsync(text, limit, cancellationToken: token),
            (text, token) => search.SearchAsync(text, limit, token),
            debounce);

    /// <summary>A controller for marketplace listings, cheapest first, with filters.</summary>
    /// <param name="search">The service to search with.</param>
    /// <param name="currentFilters">
    /// Returns the filters to use. Called on a background thread at the moment each search runs, so it must
    /// only read a value the screen stores for it (an immutable <see cref="ListingFilters"/>), never a control.
    /// </param>
    /// <param name="limit">How many listings each search returns at most.</param>
    /// <param name="debounce">How long to wait after the last keystroke.</param>
    public static LiveSearchController<ListingSearchPage> For(ListingSearchService search, Func<ListingFilters> currentFilters,
        int limit = ListingSearchService.DefaultLimit, TimeSpan? debounce = null) =>
        new(
            (text, token) => search.QuickSearchAsync(text, currentFilters(), limit, cancellationToken: token),
            (text, token) => search.SearchAsync(text, currentFilters(), limit, token),
            debounce);
}
