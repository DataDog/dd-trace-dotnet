// <copyright file="OpenTelemetrySqliteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETCOREAPP2_1

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    // Covers the ADO.NET system-test coverage for the OpenTelemetry database semantic conventions.
    // SQLite is used because it needs no database server, so the attributes and the span name are
    // the same on every machine. The cases that SQLite cannot express (stored procedures, table
    // direct commands, and a networked server) are covered by DbScopeFactoryTests instead.
    public class OpenTelemetrySqliteTests : TracingIntegrationTest
    {
        private const string ExpectedOperationName = "sqlite.query";

        // The sample connects with "Data Source=Sqlite-Test;Mode=Memory;Cache=Shared", and the data
        // source is what identifies an embedded database
        private const string ExpectedDbNamespace = "Sqlite-Test";

        // One span per command the sample executes. The nested calls a provider makes for a single
        // command (ExecuteScalar and ExecuteNonQuery both go through ExecuteReader in
        // Microsoft.Data.Sqlite) must not add any.
        private const int ExpectedSpanCount = 8;

        public OpenTelemetrySqliteTests(ITestOutputHelper output)
            : base("OpenTelemetry.Sqlite", output)
        {
            SetServiceVersion("1.0.0");
            UseNativeLibraryAlpineWorkaround();
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsSqlite(metadataSchemaVersion);

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public async Task SubmitsTracesWithOpenTelemetrySemantics()
        {
            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", "true");

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent);
            var spans = await agent.WaitForSpansAsync(ExpectedSpanCount, operationName: ExpectedOperationName);

            spans.Should().HaveCount(ExpectedSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "otel", expectedServiceName: $"{EnvironmentHelper.FullSampleName}-sqlite", isExternalSpan: true);

            foreach (var span in spans)
            {
                span.Tags.Should().Contain("db.system.name", "sqlite");
                span.Tags.Should().Contain("db.namespace", ExpectedDbNamespace);

                // SQLite is embedded, so there is no server to report
                span.Tags.Keys.Should().NotContain(["server.address", "server.port"]);

                // The Datadog names must not be reported alongside the OpenTelemetry ones
                span.Tags.Keys.Should().NotContain(["db.type", "db.name", "db.user", "out.host", "peer.service"]);

                // No query summary is available without a SQL parser, and SQLite has neither stored
                // procedures nor table direct commands, so the span name is the namespace
                span.Resource.Should().Be(ExpectedDbNamespace);
                span.Tags.Keys.Should().NotContain(["db.query.summary", "db.operation.name", "db.stored_procedure.name", "db.collection.name"]);
            }

            var queries = spans.Select(s => s.Tags["db.query.text"]).ToList();

            // Every literal is replaced with a placeholder before the query text is reported
            queries.Should().NotContainMatch("*Alice*").And.NotContainMatch("*90000*").And.NotContainMatch("*50000*");
            queries.Should().Contain("INSERT INTO Employees (Id, Name, Salary) VALUES (?, ?, ?)");
            queries.Should().Contain("SELECT Id, Name FROM Employees WHERE Name = ?");
            queries.Should().Contain("SELECT Name FROM Employees WHERE Id IN (?, ?, ?)");

            // A parameterized statement keeps its placeholders, which are not sensitive
            queries.Should().Contain("INSERT INTO Employees (Id, Name, Salary) VALUES (@id, @name, @salary)");

            var failedSpan = spans.Single(s => s.Error == 1);
            failedSpan.Tags.Should().Contain("error.type", "Microsoft.Data.Sqlite.SqliteException");

            // SQLITE_ERROR, which is what "no such table" is reported as
            failedSpan.Tags.Should().Contain("db.response.status_code", "1");

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Sqlite);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public async Task SubmitsTracesWithDatadogSemantics()
        {
            // The same sample without the feature flag, to pin that the Datadog output is unchanged
            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent);
            var spans = await agent.WaitForSpansAsync(ExpectedSpanCount, operationName: ExpectedOperationName);

            spans.Should().HaveCount(ExpectedSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: $"{EnvironmentHelper.FullSampleName}-sqlite", isExternalSpan: true);

            foreach (var span in spans)
            {
                span.Tags.Should().Contain("db.type", "sqlite");
                span.Tags.Should().Contain("out.host", ExpectedDbNamespace);

                // The query is reported in the resource name, unobfuscated: the agent obfuscates it
                span.Resource.Should().NotBeNullOrEmpty().And.NotBe(ExpectedDbNamespace);

                var openTelemetryOnlyTags = new List<string>
                {
                    "db.system.name", "db.namespace", "db.query.text", "db.query.summary",
                    "db.operation.name", "db.stored_procedure.name", "db.collection.name",
                    "db.response.status_code", "server.address", "server.port",
                };

                span.Tags.Keys.Should().NotContain(openTelemetryOnlyTags);
            }

            spans.Should().Contain(s => s.Resource == "INSERT INTO Employees (Id, Name, Salary) VALUES (1, 'Alice', 90000)");

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Sqlite);
        }
    }
}

#endif
