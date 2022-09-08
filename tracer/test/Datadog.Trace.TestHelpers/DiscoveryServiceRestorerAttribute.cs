// <copyright file="DiscoveryServiceRestorerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Agent.DiscoveryService;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    internal class DiscoveryServiceRestorerAttribute : BeforeAfterTestAttribute
    {
        private DiscoveryService _discoveryService;

        public override void Before(MethodInfo methodUnderTest)
        {
            _discoveryService = DiscoveryService.Instance;
            DiscoveryService.SetDiscoveryService(null);
            base.Before(methodUnderTest);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            DiscoveryService.SetDiscoveryService(_discoveryService);
            base.After(methodUnderTest);
        }
    }
}
