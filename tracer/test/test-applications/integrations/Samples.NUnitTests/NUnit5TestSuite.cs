#if NUNIT_5_0

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Samples.NUnit5Tests;

public class FeaturesFixture
{
    private int _repeatCount;

    public static IEnumerable<TestCaseDataWithReturn<int, int, int>> Cases()
    {
        yield return TestCaseData.Create(1, 2).SetName("CustomAddition").Returns(3);
        yield return TestCaseData.Create(2, 3).Returns(5);
    }

    [Test]
    public async Task AsyncAssertionPass()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("Expected exception");
        });
    }

    [Test]
    public async Task AsyncAssertionFailure()
    {
        await Assert.DoesNotThrowAsync(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("Unexpected async exception");
        });
    }

    [TestCaseSource(nameof(Cases))]
    public int GenericData(int left, int right) => left + right;

    [Test, Repeat(4, RequiredPassPercentage = 50)]
    public void NativeRepeatWithThreshold()
    {
        if (++_repeatCount <= 2)
        {
            Assert.Fail("An intermediate iteration failed");
        }
    }

    [Test, MaxTime(60000, WarningTime = 1)]
    public void Warning()
    {
        Thread.Sleep(20);
    }
}

public class DependenciesFixture
{
    [Test]
    public void FailedPrerequisite() => Assert.Fail("The prerequisite failed");

    [Test, DependsOnTest(nameof(FailedPrerequisite))]
    public void SkippedAfterFailure() => Assert.Fail("This dependent must not execute");

    [Test, DependsOnTest(nameof(FailedPrerequisite), AllowFailure = true)]
    public void AllowedAfterFailure()
    {
    }

    [Test, Ignore("The prerequisite is ignored")]
    public void IgnoredPrerequisite()
    {
    }

    [Test, DependsOnTest(nameof(IgnoredPrerequisite))]
    public void SkippedAfterIgnored() => Assert.Fail("This dependent must not execute");

    [Test]
    public void PassedPrerequisite()
    {
    }

    [Test, DependsOnTest(nameof(PassedPrerequisite))]
    public void PassedDependent()
    {
    }

    [Test]
    public void Independent()
    {
    }
}

public class FailingFixture
{
    [Test]
    public void Failed() => Assert.Fail("The prerequisite fixture failed");
}

[DependsOnFixture(typeof(FailingFixture))]
public class DependentFixture
{
    [TestCase(1), TestCase(2)]
    public void SkippedFixtureCase(int value) => Assert.Fail("This dependent fixture must not execute");

    [Test, Ignore("An unrelated ignore reason")]
    public void IgnoredFixtureCase()
    {
    }
}

#endif
