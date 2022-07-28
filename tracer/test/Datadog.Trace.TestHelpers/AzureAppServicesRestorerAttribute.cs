// <copyright file="AzureAppServicesRestorerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.PlatformHelpers;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class AzureAppServicesRestorerAttribute : BeforeAfterTestAttribute
    {
        private AzureAppServices _metadata;

        public override void Before(MethodInfo methodUnderTest)
        {
            _metadata = AzureAppServices.Metadata;
            base.Before(methodUnderTest);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            AzureAppServices.Metadata = _metadata;
            base.After(methodUnderTest);
        }
    }
}
