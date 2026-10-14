using System.Collections.Concurrent;
using CatchIt.Core.Data;
using CatchIt.Core.Search;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for <see cref="LiveSearchController"/>: waiting briefly while the user types, running the
/// search in the background, cancelling stale searches, and never delivering old results.
/// </summary>
/// <remarks>
/// The searches are replaced by fakes that the test controls: each fake search waits for the test
/// to release it, so the tests decide exactly when a slow search "finishes" and no test depends on
/// how fast the machine is. Waiting for something that should NOT happen uses a short pause.
/// </remarks>
public class LiveSearchControllerTests
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(300);

    private static SearchPage PageFor(string? text) =>
        new(new[] { new ProductHit(1, $"result for {text}", "Brand", 2) }, 1);

    /// <summary>A fake search that records every call and finishes only when the test says so.</summary>
    private sealed class FakeSearch
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<SearchPage>> _gates = new();
        public ConcurrentQueue<string> Calls { get; } = new();
        public ConcurrentDictionary<string, TaskCompletionSource> Started { get; } = new();

        public bool Gated { get; init; } = true;

        public async Task<SearchPage> Run(string? text, CancellationToken token)
        {
            var key = text ?? "";
            Calls.Enqueue(key);
            Started.GetOrAdd(key, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            if (!Gated) return PageFor(text);

            var gate = _gates.GetOrAdd(key, _ => new TaskCompletionSource<SearchPage>(TaskCreationOptions.RunContinuationsAsynchronously));
            return await gate.Task.WaitAsync(token);   // cancelled searches stop waiting, like a real cancellable call
        }

        public Task WaitUntilStarted(string text) =>
            Started.GetOrAdd(text, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task.WaitAsync(Patience);

        public void Finish(string text) =>
            _gates.GetOrAdd(text, _ => new TaskCompletionSource<SearchPage>(TaskCreationOptions.RunContinuationsAsynchronously))
                  .TrySetResult(PageFor(text));

        public void Fail(string text, Exception ex) =>
            _gates.GetOrAdd(text, _ => new TaskCompletionSource<SearchPage>(TaskCreationOptions.RunContinuationsAsynchronously))
                  .TrySetException(ex);
    }

    /// <summary>Collects everything a controller raises, so tests can wait for it or check that nothing came.</summary>
    private sealed class Recorder
    {
        private readonly BlockingCollection<LiveSearchResult<SearchPage>> _results = new();
        public ConcurrentQueue<Exception> Failures { get; } = new();
        public ConcurrentQueue<int> ThreadIds { get; } = new();

        public Recorder(LiveSearchController<SearchPage> controller)
        {
            controller.ResultsReady += (_, r) => { ThreadIds.Enqueue(Environment.CurrentManagedThreadId); _results.Add(r); };
            controller.SearchFailed += (_, ex) => Failures.Enqueue(ex);
        }

        public LiveSearchResult<SearchPage> Next() =>
            _results.TryTake(out var result, Patience) ? result : throw new TimeoutException("No search results arrived in time.");

        /// <summary>True if no further result arrives during a short quiet period.</summary>
        public bool NothingMoreComes() => !_results.TryTake(out _, QuietPeriod);
    }

    private static (LiveSearchController<SearchPage> Controller, FakeSearch Quick, FakeSearch Full, Recorder Recorder) Make(
        bool gated = true, TimeSpan? debounce = null)
    {
        var quick = new FakeSearch { Gated = gated };
        var full = new FakeSearch { Gated = gated };
        var controller = new LiveSearchController<SearchPage>(quick.Run, full.Run, debounce ?? Debounce);
        return (controller, quick, full, new Recorder(controller));
    }

    // ---- Typing ----

    [Fact]
    public void TextChanged_DeliversQuickResultsAfterTheDelay()
    {
        var (controller, _, _, recorder) = Make(gated: false);

        controller.TextChanged("wire");

        var result = recorder.Next();
        Assert.Equal("wire", result.Text);
        Assert.False(result.IsFinal);
        Assert.Equal("result for wire", result.Page.Hits.Single().Name);
    }

    [Fact]
    public void TextChanged_FastTyping_RunsOnlyOneSearchForTheFinalText()
    {
        var (controller, quick, _, recorder) = Make(gated: false, debounce: TimeSpan.FromMilliseconds(250));

        controller.TextChanged("w");
        controller.TextChanged("wi");
        controller.TextChanged("wir");
        controller.TextChanged("wire");

        Assert.Equal("wire", recorder.Next().Text);
        Assert.True(recorder.NothingMoreComes());
        Assert.Equal(new[] { "wire" }, quick.Calls.ToArray()); // the earlier letters never reached the database
    }

    [Fact]
    public async Task TextChanged_AnOlderSlowSearchNeverOverwritesNewerResults()
    {
        var (controller, quick, _, recorder) = Make();

        controller.TextChanged("wi");
        await quick.WaitUntilStarted("wi");       // the first search is now "running" and slow
        controller.TextChanged("wire");           // the user kept typing
        await quick.WaitUntilStarted("wire");

        quick.Finish("wire");                     // the newer search finishes first...
        Assert.Equal("wire", recorder.Next().Text);

        quick.Finish("wi");                       // ...then the stale one finally finishes
        Assert.True(recorder.NothingMoreComes()); // and must be ignored
    }

    [Fact]
    public async Task TextChanged_AStaleSearchThatFinishesBeforeTheNewerOne_IsStillIgnored()
    {
        var (controller, quick, _, recorder) = Make();

        controller.TextChanged("wi");
        await quick.WaitUntilStarted("wi");
        controller.TextChanged("wire");
        await quick.WaitUntilStarted("wire");

        quick.Finish("wi");                       // the stale one completes first
        quick.Finish("wire");

        Assert.Equal("wire", recorder.Next().Text);
        Assert.True(recorder.NothingMoreComes());
    }

    // ---- Submitting ----

    [Fact]
    public void Submit_SearchesImmediatelyWithTheFullSearch_EvenWithALongDelay()
    {
        var (controller, quick, full, recorder) = Make(gated: false, debounce: TimeSpan.FromSeconds(30));

        controller.Submit("wireless");

        var result = recorder.Next();
        Assert.Equal("wireless", result.Text);
        Assert.True(result.IsFinal);
        Assert.Empty(quick.Calls);
        Assert.Equal(new[] { "wireless" }, full.Calls.ToArray());
    }

    [Fact]
    public async Task Submit_CancelsAnInProgressQuickSearch()
    {
        var (controller, quick, full, recorder) = Make();

        controller.TextChanged("wireless");
        await quick.WaitUntilStarted("wireless");
        controller.Submit("wireless");
        await full.WaitUntilStarted("wireless");

        quick.Finish("wireless");                  // the quick one finishes after being superseded
        full.Finish("wireless");

        var result = recorder.Next();
        Assert.True(result.IsFinal);
        Assert.True(recorder.NothingMoreComes());  // the quick result was never delivered
    }

    [Fact]
    public async Task TextChanged_AfterASubmit_ReplacesTheFullResults()
    {
        var (controller, quick, full, recorder) = Make();

        controller.Submit("wireless");
        await full.WaitUntilStarted("wireless");
        controller.TextChanged("monitor");
        await quick.WaitUntilStarted("monitor");

        full.Finish("wireless");
        quick.Finish("monitor");

        Assert.Equal("monitor", recorder.Next().Text);
        Assert.True(recorder.NothingMoreComes());
    }

    // ---- Cancelling and stopping ----

    [Fact]
    public async Task Cancel_PreventsResultsFromBeingDelivered()
    {
        var (controller, quick, _, recorder) = Make();

        controller.TextChanged("wire");
        await quick.WaitUntilStarted("wire");
        controller.Cancel();
        quick.Finish("wire");

        Assert.True(recorder.NothingMoreComes());
    }

    [Fact]
    public async Task Dispose_StopsDeliveryAndIgnoresLaterRequests()
    {
        var (controller, quick, full, recorder) = Make();

        controller.TextChanged("wire");
        await quick.WaitUntilStarted("wire");
        controller.Dispose();
        quick.Finish("wire");

        controller.TextChanged("more");
        controller.Submit("more");

        Assert.True(recorder.NothingMoreComes());
        Assert.DoesNotContain("more", quick.Calls);
        Assert.Empty(full.Calls);
    }

    [Fact]
    public void Cancel_ThenANewRequest_StillWorks()
    {
        var (controller, _, _, recorder) = Make(gated: false);

        controller.Cancel();
        controller.TextChanged("again");

        Assert.Equal("again", recorder.Next().Text);
    }

    // ---- Failures ----

    [Fact]
    public async Task AFailedSearch_RaisesSearchFailed_AndNoResults()
    {
        var (controller, quick, _, recorder) = Make();

        controller.TextChanged("wire");
        await quick.WaitUntilStarted("wire");
        quick.Fail("wire", new InvalidOperationException("the database is unavailable"));

        Assert.True(recorder.NothingMoreComes());
        Assert.Equal("the database is unavailable", Assert.Single(recorder.Failures).Message);
    }

    [Fact]
    public async Task AFailureInASupersededSearch_IsNotReported()
    {
        var (controller, quick, _, recorder) = Make();

        controller.TextChanged("wi");
        await quick.WaitUntilStarted("wi");
        controller.TextChanged("wire");
        await quick.WaitUntilStarted("wire");

        quick.Fail("wi", new InvalidOperationException("boom"));
        quick.Finish("wire");

        Assert.Equal("wire", recorder.Next().Text);
        Assert.Empty(recorder.Failures);
    }

    [Fact]
    public void ANullSearchText_IsPassedThroughAsEmpty()
    {
        var (controller, _, _, recorder) = Make(gated: false);

        controller.TextChanged(null);

        Assert.Equal("", recorder.Next().Text);
    }

    // ---- Delivering on the screen's thread ----

    /// <summary>A stand-in for the UI thread: runs everything posted to it on one dedicated thread.</summary>
    private sealed class SingleThreadContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback, object?)> _queue = new();
        private readonly Thread _thread;

        public int ThreadId => _thread.ManagedThreadId;

        public SingleThreadContext()
        {
            _thread = new Thread(() =>
            {
                foreach (var (callback, state) in _queue.GetConsumingEnumerable())
                    callback(state);
            }) { IsBackground = true };
            _thread.Start();
        }

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public void Dispose() => _queue.CompleteAdding();
    }

    [Fact]
    public void Results_AreRaisedOnTheThreadTheControllerWasCreatedOn()
    {
        using var ui = new SingleThreadContext();
        LiveSearchController<SearchPage>? controller = null;
        Recorder? recorder = null;
        var created = new ManualResetEventSlim();

        // Build the controller "on the UI thread", as a form would in its constructor.
        ui.Post(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(ui);
            controller = new LiveSearchController<SearchPage>(new FakeSearch { Gated = false }.Run, new FakeSearch { Gated = false }.Run, Debounce);
            recorder = new Recorder(controller);
            created.Set();
        }, null);
        Assert.True(created.Wait(Patience));

        controller!.TextChanged("wire");
        var result = recorder!.Next();

        Assert.Equal("wire", result.Text);
        Assert.Equal(ui.ThreadId, Assert.Single(recorder.ThreadIds));  // the handler ran on the UI thread
    }

    // ---- With the real search service ----

    [Fact]
    public void WithTheRealService_TypingGivesQuickSuggestionsAndSubmitGivesRankedResults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
        try
        {
            var db = new Database(path);
            db.Initialize();
            TestCatalog.AddProduct(db, "Apex Wireless Headphones", "Apex", 3, "headphones wireless");
            TestCatalog.AddProduct(db, "Orbit Wireless Headphones", "Orbit", 2, "headphones wireless");
            TestCatalog.AddProduct(db, "Zenith Monitor", "Zenith", 3, "monitor");

            var controller = LiveSearchController.For(new ProductSearchService(db), debounce: Debounce);
            var recorder = new Recorder(controller);

            controller.TextChanged("wireless head");
            var quick = recorder.Next();
            controller.Submit("wireless head");
            var final = recorder.Next();

            Assert.False(quick.IsFinal);
            Assert.True(final.IsFinal);
            Assert.Equal(2, quick.Page.TotalMatches);
            Assert.Equal(2, final.Page.TotalMatches);
            Assert.True(quick.Page.Hits.Select(h => h.ProductId).ToHashSet().SetEquals(final.Page.Hits.Select(h => h.ProductId)));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
