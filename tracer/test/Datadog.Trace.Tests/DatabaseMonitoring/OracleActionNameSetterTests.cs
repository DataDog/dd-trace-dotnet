// <copyright file="OracleActionNameSetterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DatabaseMonitoring;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DatabaseMonitoring
{
    public class OracleActionNameSetterTests
    {
        [Theory]
        [InlineData(typeof(OracleConnection))]
        [InlineData(typeof(DerivedOracleConnection))]
        public void SetsActionName(Type connectionType)
        {
            var connection = (OracleConnection)Activator.CreateInstance(connectionType)!;

            var setActionName = OracleActionNameSetter.Get(connectionType);

            setActionName.Should().NotBeNull();
            setActionName!(connection, "_DD_DDSH:abc");
            connection.LastActionName.Should().Be("_DD_DDSH:abc");
            setActionName(connection, string.Empty);
            connection.LastActionName.Should().BeEmpty();
        }

        [Theory]
        [InlineData(typeof(NoActionNameConnection))]
        [InlineData(typeof(ReadOnlyActionNameConnection))]
        [InlineData(typeof(PrivateSetterActionNameConnection))]
        [InlineData(typeof(IntActionNameConnection))]
        public void ReturnsNullWithoutAPublicWritableStringActionName(Type connectionType)
        {
            OracleActionNameSetter.Get(connectionType).Should().BeNull();
            OracleActionNameSetter.Get(connectionType).Should().BeNull();
        }

        [Fact]
        public async Task ReturnsTheSameDelegateOnConcurrentFirstUse()
        {
            using var start = new ManualResetEventSlim();
            var tasks = Enumerable.Range(0, 16)
                                  .Select(_ => Task.Run(() =>
                                   {
                                       start.Wait();
                                       return OracleActionNameSetter.Get(typeof(ConcurrentOracleConnection));
                                   }))
                                  .ToArray();

            start.Set();
            var setters = await Task.WhenAll(tasks);

            setters.Should().OnlyContain(s => s != null && ReferenceEquals(s, setters[0]));
        }

        public class OracleConnection
        {
            public string LastActionName { get; private set; }

            // ODP.NET exposes ActionName as a write-only property
            public string ActionName
            {
                set => LastActionName = value;
            }
        }

        public class DerivedOracleConnection : OracleConnection
        {
        }

        public class ConcurrentOracleConnection : OracleConnection
        {
        }

        public class NoActionNameConnection
        {
        }

        public class ReadOnlyActionNameConnection
        {
            public string ActionName => "read-only";
        }

        public class PrivateSetterActionNameConnection
        {
            public string ActionName { get; private set; }
        }

        public class IntActionNameConnection
        {
            public int ActionName
            {
                set => _ = value;
            }
        }
    }
}
