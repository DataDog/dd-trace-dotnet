// <copyright file="IAspNetFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers;

/// <summary>
/// The parts of an HTTP-server test fixture that a harness shared by several hosting models needs:
/// the ddapm test-agent session the application under test exports OTLP to, where the fixture
/// should write its diagnostics, and a once-per-test-class initialization point.
/// </summary>
public interface IAspNetFixture
{
    /// <summary>
    /// Gets the ddapm test-agent session the application under test exports OTLP to. Owned by the
    /// fixture rather than by each test case, because the session token is baked into the
    /// application's environment when it starts and the application is shared by every test case in
    /// the class, so a token generated per test case would stop matching what the running
    /// application actually sends.
    /// </summary>
    OtlpTestAgentSession OtlpSession { get; }

    /// <summary>
    /// Points the fixture's diagnostics at the test case that is currently running, or at nothing
    /// when <paramref name="output"/> is <c>null</c>. The fixture - and, with it, the application's
    /// stdout/stderr handlers - outlives every individual test case, while an
    /// <see cref="ITestOutputHelper"/> only accepts writes until its own test case ends, so the
    /// current one has to be swapped in and out rather than captured once.
    /// </summary>
    /// <param name="output">The current test case's output helper, or <c>null</c> when no test case is running.</param>
    void SetOutput(ITestOutputHelper? output);

    /// <summary>
    /// Runs <paramref name="initialize"/> the first time it is called and awaits that same task on
    /// every later call, so a test class's one-time setup happens once no matter how many test cases
    /// it has. xUnit builds a fresh instance of the test class - and so runs
    /// <c>IAsyncLifetime.InitializeAsync</c> - for every test case, while the fixture is created
    /// once per test class, which makes the fixture the only place such a latch can live.
    /// </summary>
    /// <param name="initialize">The setup to run once. A failure is cached along with the task, so
    /// the remaining test cases fail with the same exception instead of each retrying a setup that
    /// has already been shown not to work.</param>
    /// <returns>The single initialization task, shared by every test case in the class.</returns>
    Task EnsureInitializedAsync(Func<Task> initialize);
}
