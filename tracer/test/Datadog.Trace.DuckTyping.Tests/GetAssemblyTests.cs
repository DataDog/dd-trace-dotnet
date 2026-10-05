// <copyright file="GetAssemblyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    [Collection(nameof(GetAssemblyTestsCollection))]
    public class GetAssemblyTests
    {
        private interface IAssemblyEnumerationProxy
        {
            int Value { get; }
        }

        [Fact]
        [Trait("SkipInCI", "True")]
        public void GetAssemblyTest()
        {
            // Validate a known proxy so filtered runs exercise type enumeration too. The global assembly
            // count varies with the test inventory, execution order, and CI Visibility instrumentation.
            var proxy = DuckType.Create<IAssemblyEnumerationProxy>(new AssemblyEnumerationTarget());
            proxy!.Value.Should().Be(42);
            var proxyType = proxy.GetType();
            var proxyAssembly = proxyType.Assembly;
            var asmDuckTypes = 0;
            var proxyAssemblyEnumerated = false;
            var lstExceptions = new List<Exception>();
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            assemblies.Should().Contain(proxyAssembly);
            foreach (var assembly in assemblies)
            {
                if (assembly == proxyAssembly ||
                    assembly.FullName!.StartsWith(DuckTypeConstants.DuckTypeAssemblyPrefix) ||
                    assembly.FullName!.StartsWith(DuckTypeConstants.DuckTypeGenericTypeAssemblyPrefix) ||
                    assembly.FullName!.StartsWith(DuckTypeConstants.DuckTypeNotVisibleAssemblyPrefix))
                {
                    asmDuckTypes++;

                    try
                    {
                        var types = assembly.GetTypes();
                        if (assembly == proxyAssembly)
                        {
                            types.Should().Contain(proxyType);
                            proxyAssemblyEnumerated = true;
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        lstExceptions.AddRange(ex.LoaderExceptions);
                    }
                }
            }

            if (lstExceptions.Count > 0)
            {
                throw new AggregateException(lstExceptions.ToArray());
            }

            proxyAssemblyEnumerated.Should().BeTrue();
            asmDuckTypes.Should().BeGreaterThan(0);
        }

        private sealed class AssemblyEnumerationTarget
        {
            public int Value => 42;
        }
    }
}
