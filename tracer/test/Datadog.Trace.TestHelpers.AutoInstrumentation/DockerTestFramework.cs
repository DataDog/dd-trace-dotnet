// <copyright file="DockerTestFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation;

public class DockerTestFramework : CustomTestFramework
{
    public DockerTestFramework(IMessageSink messageSink)
        : base(messageSink)
    {
    }

    public DockerTestFramework(IMessageSink messageSink, Type typeTestedAssembly)
        : base(messageSink, typeTestedAssembly)
    {
    }

    protected override async Task RunTestCollectionsCallback(IMessageSink diagnosticsMessageSink, IEnumerable<IXunitTestCase> testCases)
    {
        var containerFixtures = testCases
                               .Select(t => t.Method.ToRuntimeMethod().DeclaringType)
                               .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IClassFixture<>)))
                               .ToList();

        foreach (var type in containerFixtures)
        {
            // Retrieve all the types of container fixtures
            var fixtureTypes = type.GetInterfaces()
                                   .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IClassFixture<>))
                                   .Select(i => i.GetGenericArguments()[0])
                                   .Where(t => typeof(ContainerFixture).IsAssignableFrom(t))
                                   .ToList();

            foreach (var fixtureType in fixtureTypes)
            {
                try
                {
                    var fixture = (ContainerFixture)Activator.CreateInstance(fixtureType);
                    await fixture!.InitializeAsync();
                }
                catch (Exception ex)
                {
                    DiagnosticMessageSink.OnMessage(new DiagnosticMessage($"ERROR: {fixtureType.Name} ({ex.Message})"));
                }
            }
        }
    }
}
