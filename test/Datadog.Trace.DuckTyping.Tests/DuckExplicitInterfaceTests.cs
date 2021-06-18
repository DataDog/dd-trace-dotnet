// <copyright file="DuckExplicitInterfaceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1201 // Elements must appear in the correct order

using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    public class DuckExplicitInterfaceTests
    {
        [Fact]
        public void NormalTest()
        {
            var targetObject = new TargetObject();
            var proxy = targetObject.DuckCast<IProxyDefinition>();

            proxy.SayHi().Should().Equals("Hello World");
            proxy.SayHiWithWildcard().Should().Equals("Hello World (*)");
        }

        public class TargetObject : ITarget
        {
            string ITarget.SayHi()
            {
                return "Hello World";
            }

            string ITarget.SayHiWithWildcard()
            {
                return "Hello World (*)";
            }
        }

        public interface ITarget
        {
            string SayHi();

            string SayHiWithWildcard();
        }

        public interface IProxyDefinition
        {
            [Duck(ExplicitInterfaceTypeName = "Datadog.Trace.DuckTyping.Tests.DuckExplicitInterfaceTests+ITarget")]
            string SayHi();

            [Duck(ExplicitInterfaceTypeName = "*")]
            string SayHiWithWildcard();
        }
    }
}
