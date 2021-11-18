// <copyright file="ScopeFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using Moq;
using MySql.Data.MySqlClient;
using Npgsql;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ScopeFactoryTests
    {
        // declare here instead of using ScopeFactory.UrlIdPlaceholder so tests fails if value changes
        private const string Id = "?";

        public static IEnumerable<object[]> GetDbCommandScopeData()
        {
            yield return new object[] { new System.Data.SqlClient.SqlCommand() };
            yield return new object[] { new MySqlCommand() };
            yield return new object[] { new NpgsqlCommand() };
            yield return new object[] { new Microsoft.Data.SqlClient.SqlCommand() };
        }

        [Theory]
        [InlineData("users/", "users/")]
        [InlineData("users", "users")]
        [InlineData("123/", Id + "/")]
        [InlineData("123", Id)]
        [InlineData("4294967294/", Id + "/")]
        [InlineData("4294967294", Id)]
        [InlineData("E653C852-227B-4F0C-9E48-D30D83C68BF3/", Id + "/")]
        [InlineData("E653C852-227B-4F0C-9E48-D30D83C68BF3", Id)]
        [InlineData("E653C852227B4F0C9E48D30D83C68BF3/", Id + "/")]
        [InlineData("E653C852227B4F0C9E48D30D83C68BF3", Id)]
        public void CleanUriSegment(string segment, string expected)
        {
            string actual = UriHelpers.GetCleanUriPath(segment);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("https://username:password@example.com/path/to/file.aspx?query=1#fragment", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://username@example.com/path/to/file.aspx", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx?query=1", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx#fragment", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("http://example.com/path/to/file.aspx", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/123/file.aspx", "GET", "GET example.com/path/" + Id + "/file.aspx")]
        [InlineData("https://example.com/path/123/", "GET", "GET example.com/path/" + Id + "/")]
        [InlineData("https://example.com/path/123", "GET", "GET example.com/path/" + Id)]
        [InlineData("https://example.com/path/4294967294/file.aspx", "GET", "GET example.com/path/" + Id + "/file.aspx")]
        [InlineData("https://example.com/path/4294967294/", "GET", "GET example.com/path/" + Id + "/")]
        [InlineData("https://example.com/path/4294967294", "GET", "GET example.com/path/" + Id)]
        [InlineData("https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3", "GET", "GET example.com/path/" + Id)]
        [InlineData("https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3", "GET", "GET example.com/path/" + Id)]
        public void CleanUri_ResourceName(string uri, string method, string expected)
        {
            // Set up Tracer
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();
            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            using (var automaticScope = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(uri), IntegrationId.HttpMessageHandler, out _))
            {
                Assert.Equal(expected, automaticScope.Span.ResourceName);
            }
        }

        [Fact]
        public void CreateOutboundHttpScope_Null_ResourceUri()
        {
            // Set up Tracer
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();
            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            using (var automaticScope = ScopeFactory.CreateOutboundHttpScope(tracer, "GET", null, IntegrationId.HttpMessageHandler, out _))
            {
                Assert.Equal(expected: "GET ",  actual: automaticScope.Span.ResourceName);
            }
        }

        [Theory]
        [InlineData("https://username:password@example.com/path/to/file.aspx?query=1#fragment", "https://example.com/path/to/file.aspx")]
        [InlineData("https://username@example.com/path/to/file.aspx", "https://example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx?query=1", "https://example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx#fragment", "https://example.com/path/to/file.aspx")]
        [InlineData("http://example.com/path/to/file.aspx", "http://example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/123/file.aspx", "https://example.com/path/123/file.aspx")]
        [InlineData("https://example.com/path/123/", "https://example.com/path/123/")]
        [InlineData("https://example.com/path/123", "https://example.com/path/123")]
        [InlineData("https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3", "https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3")]
        [InlineData("https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3", "https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3")]
        public void CleanUri_HttpUrlTag(string uri, string expected)
        {
            // Set up Tracer
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();
            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            const string method = "GET";

            using (var automaticScope = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(uri), IntegrationId.HttpMessageHandler, out var tags))
            {
                Assert.Equal(expected, automaticScope.Span.GetTag(Tags.HttpUrl));
                Assert.Equal(expected, tags.HttpUrl);
            }
        }

        [Theory]
        [InlineData((int)IntegrationId.HttpMessageHandler, (int)IntegrationId.HttpMessageHandler)] // This scenario may occur on any .NET runtime with nested HttpMessageHandler's and HttpSocketHandler's
        [InlineData((int)IntegrationId.WebRequest, (int)IntegrationId.HttpMessageHandler)] // This scenario may occur on .NET Core where the underlying transport for WebRequest is HttpMessageHandler
        public void CreateOutboundHttpScope_AlwaysCreatesOneAutomaticInstrumentationScope(int integration1, int integration2)
        {
            // Set up Tracer
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();
            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            const string method = "GET";
            const string url = "http://www.contoso.com";

            // Manually create a span decorated with HTTP information
            using (var manualScope = tracer.StartActive("http.request"))
            {
                manualScope.Span.Type = SpanTypes.Http;
                manualScope.Span.ResourceName = $"{method} {url}";
                manualScope.Span.ServiceName = $"{tracer.DefaultServiceName}-http-client";

                using (var automaticScope1 = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(url), (IntegrationId)integration1, out _))
                {
                    using (var automaticScope2 = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(url), (IntegrationId)integration2, out _))
                    {
                        Assert.NotNull(manualScope);
                        Assert.NotNull(automaticScope1);
                        Assert.Null(automaticScope2);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetDbCommandScopeData))]
        public void CreateDbCommandScope_ReturnsNullForExcludedAdoNetTypes(IDbCommand command)
        {
            // Set up tracer
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.AdoNetExcludedTypes, command.GetType().FullName }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);
            var tracer = TracerHelper.Create(tracerSettings);

            // Create scope
            using (var outerScope = ScopeFactory.CreateDbCommandScope(tracer, new CustomDbCommand()))
            {
                using (var innerScope = ScopeFactory.CreateDbCommandScope(tracer, command))
                {
                    Assert.Null(innerScope);
                }

                Assert.NotNull(outerScope);
            }
        }

        [Theory]
        [MemberData(nameof(GetDbCommandScopeData))]
        public void CreateDbCommandScope_UsesReplacementServiceNameWhenProvided(IDbCommand command)
        {
            // Set up tracer
            var t = command.GetType();
            var dbType = ScopeFactory.GetDbType(t.Namespace, t.Name);
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.ServiceNameMappings, $"{dbType}:my-custom-type" }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);
            var tracer = TracerHelper.Create(tracerSettings);

            // Create scope
            using (var outerScope = ScopeFactory.CreateDbCommandScope(tracer, command))
            {
                Assert.Equal("my-custom-type", outerScope.Span.ServiceName);
            }
        }

        [Theory]
        [MemberData(nameof(GetDbCommandScopeData))]
        public void CreateDbCommandScope_IgnoresReplacementServiceNameWhenNotProvided(IDbCommand command)
        {
            // Set up tracer
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.ServiceNameMappings, $"something:my-custom-type" }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);
            var tracer = TracerHelper.Create(tracerSettings);

            // Create scope
            using (var outerScope = ScopeFactory.CreateDbCommandScope(tracer, command))
            {
                Assert.NotEqual("my-custom-type", outerScope.Span.ServiceName);
            }
        }

        private static Scope SystemDataSqlClientSqlCommandCreateScope(Tracer tracer) => ScopeFactory.CreateDbCommandScope(tracer, new System.Data.SqlClient.SqlCommand());

        private static Scope PostgresCreateScope(Tracer tracer) => ScopeFactory.CreateDbCommandScope(tracer, new NpgsqlCommand());

        private static Scope CustomCreateScope(Tracer tracer) => ScopeFactory.CreateDbCommandScope(tracer, new CustomDbCommand());

        private static Scope MicrosoftDataSqlClientSqlCommandCreateScope(Tracer tracer) => ScopeFactory.CreateDbCommandScope(tracer, new Microsoft.Data.SqlClient.SqlCommand());

        private class CustomDbCommand : DbCommand
        {
            public override string CommandText { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override int CommandTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override CommandType CommandType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override bool DesignTimeVisible { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override UpdateRowSource UpdatedRowSource { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            protected override DbConnection DbConnection { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();

            protected override DbTransaction DbTransaction { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override void Cancel()
            {
                throw new NotImplementedException();
            }

            public override int ExecuteNonQuery()
            {
                throw new NotImplementedException();
            }

            public override object ExecuteScalar()
            {
                throw new NotImplementedException();
            }

            public override void Prepare()
            {
                throw new NotImplementedException();
            }

            protected override DbParameter CreateDbParameter()
            {
                throw new NotImplementedException();
            }

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            {
                throw new NotImplementedException();
            }
        }
    }
}
