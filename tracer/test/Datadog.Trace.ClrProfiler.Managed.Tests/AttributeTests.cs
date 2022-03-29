// <copyright file="AttributeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class AttributeTests
    {
        /// <summary>
        /// This test reports instances in the Datadog.Trace.ClrProfiler.Managed and Datadog.Trace
        /// where attributes use named arguments that are not a built-in type. Using a custom type
        /// (especially one from the Datadog.Trace assembly) as a named argument can fail with a
        /// System.Reflection.CustomAttributeFormatException if the owning assembly cannot be found
        /// in the default load context and other assembly resolve events are registered, in addition
        /// to the ones added by the Datadog .NET Tracer
        /// </summary>
        [Fact]
        public void AttributesInstantiationsOnlyUseBuiltinTypes()
        {
            List<string> invalidAttributeUsages = new();
            var coreAssembly = typeof(object).Assembly;

            var tracerAssembly = typeof(Tracer).Assembly;
            var managedAssembly = typeof(Instrumentation).Assembly;
            var types = tracerAssembly.GetTypes().Concat(managedAssembly.GetTypes());

            foreach (var type in types)
            {
                foreach (var member in type.GetMembers())
                {
                    foreach (var attribute in member.CustomAttributes)
                    {
                        foreach (var namedArg in attribute.NamedArguments)
                        {
                            var argumentType = namedArg.TypedValue.ArgumentType;
                            if (argumentType.Assembly != coreAssembly)
                            {
                                invalidAttributeUsages.Add($"{type.FullName}.{member.Name} => {attribute.AttributeType.FullName}({namedArg.MemberName} : {argumentType.FullName})");
                            }
                        }
                    }
                }
            }

            invalidAttributeUsages.Should().BeEmpty();
        }
    }
}
