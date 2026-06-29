// <copyright file="DbOtelHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.AdoNet;

public class DbOtelHelperTests
{
    // ── db.system.name mapping ────────────────────────────────────────────────────

    [Theory]
    [InlineData("postgres",   "postgresql")]
    [InlineData("sql-server", "microsoft.sql_server")]
    [InlineData("mysql",      "mysql")]
    [InlineData("oracle",     "oracle.db")]
    [InlineData("sqlite",     "sqlite")]
    [InlineData("someother",  "someother")]   // passthrough
    public void SetDatabaseAttributes_MapsDbSystemName(string dbType, string expectedSystemName)
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, dbType, dbName: null, outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("db.system.name").Should().Be(expectedSystemName);
    }

    // ── db.namespace ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsDbNamespace_WhenDbNameProvided()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: "mydb", outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("db.namespace").Should().Be("mydb");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsDbNamespace_WhenDbNameNull()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("db.namespace").Should().BeNull();
    }

    // ── server.address ────────────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsServerAddress_WhenOutHostProvided()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: "db.host.local", port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("server.address").Should().Be("db.host.local");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsServerAddress_WhenOutHostNull()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("server.address").Should().BeNull();
    }

    // ── server.port ───────────────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsServerPort_WhenPortProvided()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: "5432", commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("server.port").Should().Be("5432");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsServerPort_WhenPortNull()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("server.port").Should().BeNull();
    }

    // ── db.query.text ─────────────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsDbQueryText_WhenCommandTextProvided()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: "SELECT 1", peerServiceEnabled: false);

        span.GetTag("db.query.text").Should().Be("SELECT 1");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsDbQueryText_WhenCommandTextEmpty()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("db.query.text").Should().BeNull();
    }

    // ── db.operation.name + db.collection.name ────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsOperationAndCollection_FromParsedSql()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: "SELECT * FROM orders WHERE id = 1", peerServiceEnabled: false);

        span.GetTag("db.operation.name").Should().Be("SELECT");
        span.GetTag("db.collection.name").Should().Be("orders");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsCollection_WhenAmbiguous()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: "SELECT * FROM a, b", peerServiceEnabled: false);

        span.GetTag("db.operation.name").Should().Be("SELECT");
        span.GetTag("db.collection.name").Should().BeNull();
    }

    // ── peer.service ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsPeerService_WhenPeerServiceEnabled_PrefersDbName()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: "mydb", outHost: "myhost", port: null, commandText: string.Empty, peerServiceEnabled: true);

        span.GetTag("peer.service").Should().Be("mydb");
    }

    [Fact]
    public void SetDatabaseAttributes_SetsPeerService_WhenPeerServiceEnabled_FallsBackToOutHost()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: "myhost", port: null, commandText: string.Empty, peerServiceEnabled: true);

        span.GetTag("peer.service").Should().Be("myhost");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsPeerService_WhenPeerServiceDisabled()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: "mydb", outHost: "myhost", port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("peer.service").Should().BeNull();
    }

    // ── legacy names absent ───────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_DoesNotSetLegacyTagNames()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: "mydb", outHost: "myhost", port: "5432", commandText: "SELECT 1", peerServiceEnabled: false);

        span.GetTag("db.type").Should().BeNull();
        span.GetTag("db.name").Should().BeNull();
        span.GetTag("out.host").Should().BeNull();
        span.GetTag("out.port").Should().BeNull();
    }
}
