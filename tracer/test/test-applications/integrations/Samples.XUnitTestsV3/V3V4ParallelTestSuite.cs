#if XUNIT_V3_V4

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.v3;

#nullable enable

namespace Samples.XUnitTestsV3V4Parallel;

public sealed class TestSuite : IClassFixture<SharedFixture>
{
    private static readonly bool RequireCaseParallelism = string.Equals(Environment.GetEnvironmentVariable("XUNIT_V3_V4_REQUIRE_CASE_PARALLELISM"), "1", StringComparison.Ordinal);
    private static readonly ConcurrentDictionary<int, int> TheoryAttempts = new();
    private static readonly ParallelGate FactGate = new(participantCount: 2, RequireCaseParallelism);
    private static readonly ParallelGate TheoryGate = new(participantCount: 4, RequireCaseParallelism);
    private static int _retryCount;
    private static int _cancellationContextAttempt = -1;
    private static int _trueAtLastRetryCount = -1;
    private static int _trueAtThirdRetryCount = -1;
    private readonly SharedFixture _fixture;
    private readonly ITestOutputHelper _output;

    static TestSuite()
    {
        int.TryParse(Environment.GetEnvironmentVariable("DD_CIVISIBILITY_FLAKY_RETRY_COUNT"), out _retryCount);
    }

    public TestSuite(SharedFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task AlwaysPasses()
    {
        await FactGate.WaitAsync();
        AssertFixtureWasPreserved();
        _output.WriteLine("AlwaysPasses");
    }

    [Fact]
    public async Task AlwaysFails()
    {
        await FactGate.WaitAsync();
        AssertFixtureWasPreserved();
        _output.WriteLine("AlwaysFails");
        Assert.Fail("Expected failure used to exercise ATR");
    }

    [Fact]
    public void TrueAtLastRetry()
    {
        AssertFixtureWasPreserved();
        Assert.Equal(_retryCount, Interlocked.Increment(ref _trueAtLastRetryCount));
    }

    [Fact]
    public void TrueAtThirdRetry()
    {
        AssertFixtureWasPreserved();
        Assert.Equal(3, Interlocked.Increment(ref _trueAtThirdRetryCount));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ConcurrentTheoryRow(int row)
    {
        var attempt = TheoryAttempts.AddOrUpdate(row, 1, static (_, current) => current + 1);
        if (attempt == 1)
        {
            await TheoryGate.WaitAsync();
        }

        AssertFixtureWasPreserved();
        _output.WriteLine("row={0};attempt={1}", row, attempt);
        Assert.True(attempt > 1, $"First execution for row {row} intentionally fails");
    }

    [Fact]
    public void DynamicSkip()
    {
        Assert.Skip("Dynamic skip from the xUnit v3/v4 parallel sample");
    }

    [Fact]
    [MethodLifecycle]
    public void CancellationContextIsAvailableOnRetry()
    {
        AssertFixtureWasPreserved();
        var cancellationToken = TestContext.Current.CancellationToken;
        MethodLifecycleAttribute.AssertActive(TestContext.Current.Test?.UniqueID, cancellationToken);

        if (Interlocked.Increment(ref _cancellationContextAttempt) == 0)
        {
            Assert.Fail("The first execution intentionally fails to exercise retry context propagation");
        }
    }

    private void AssertFixtureWasPreserved()
    {
        Assert.Equal(1, SharedFixture.ConstructionCount);
        Assert.Equal(1, _fixture.Id);
    }
}

public sealed class SharedFixture : IAsyncDisposable
{
    private static int _constructionCount;
    private int _disposed;

    public SharedFixture()
    {
        Id = Interlocked.Increment(ref _constructionCount);
    }

    public static int ConstructionCount => Volatile.Read(ref _constructionCount);

    public int Id { get; }

    public ValueTask DisposeAsync()
    {
        Assert.Equal(0, Interlocked.Exchange(ref _disposed, 1));
        Assert.Equal(1, ConstructionCount);
        return default;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class MethodLifecycleAttribute : BeforeAfterTestAttribute
{
    private static readonly AsyncLocal<CancellationToken> ActiveCancellationToken = new();
    private static readonly AsyncLocal<string?> ActiveTestId = new();

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        Assert.Null(ActiveTestId.Value);
        Assert.Equal(default, ActiveCancellationToken.Value);
        ActiveTestId.Value = test.UniqueID;
        ActiveCancellationToken.Value = TestContext.Current.CancellationToken;
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        Assert.Equal(test.UniqueID, ActiveTestId.Value);
        Assert.Equal(TestContext.Current.CancellationToken, ActiveCancellationToken.Value);
        ActiveTestId.Value = null;
        ActiveCancellationToken.Value = default;
    }

    public static void AssertActive(string? testId, CancellationToken cancellationToken)
    {
        Assert.Equal(testId, ActiveTestId.Value);
        Assert.Equal(cancellationToken, ActiveCancellationToken.Value);
    }
}

internal sealed class ParallelGate
{
    private readonly bool _enabled;
    private readonly int _participantCount;
    private readonly TaskCompletionSource<object?> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    public ParallelGate(int participantCount, bool enabled)
    {
        _participantCount = participantCount;
        _enabled = enabled;
    }

    public async Task WaitAsync()
    {
        if (!_enabled)
        {
            return;
        }

        if (Interlocked.Increment(ref _arrived) >= _participantCount)
        {
            _ready.TrySetResult(null);
        }

        await _ready.Task;
    }
}

[Collection("shared-v4-collection")]
public sealed class CollectionSharedFirst
{
    private readonly ITestOutputHelper _output;

    public CollectionSharedFirst(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PassesInSharedCollection() => _output.WriteLine("shared-collection-first");
}

[Collection("shared-v4-collection")]
public sealed class CollectionSharedSecond
{
    private readonly ITestOutputHelper _output;

    public CollectionSharedSecond(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PassesInSharedCollection() => _output.WriteLine("shared-collection-second");
}

[Collection("independent-v4-collection-a")]
public sealed class CollectionIndependentFirst
{
    private readonly ITestOutputHelper _output;

    public CollectionIndependentFirst(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PassesInIndependentCollection() => _output.WriteLine("independent-collection-first");
}

[Collection("independent-v4-collection-b")]
public sealed class CollectionIndependentSecond
{
    private readonly ITestOutputHelper _output;

    public CollectionIndependentSecond(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PassesInIndependentCollection() => _output.WriteLine("independent-collection-second");
}

#endif
