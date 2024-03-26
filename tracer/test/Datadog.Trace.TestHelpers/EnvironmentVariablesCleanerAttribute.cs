// <copyright file="EnvironmentVariablesCleanerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Util;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class EnvironmentVariablesCleanerAttribute : BeforeAfterTestAttribute
    {
        private string[] _environmentVariables;

        public EnvironmentVariablesCleanerAttribute(params string[] environmentKeys)
        {
            _environmentVariables = environmentKeys;
        }

        public override void Before(MethodInfo methodUnderTest)
        {
            foreach (var key in _environmentVariables)
            {
                EnvironmentHelpers.SetEnvironmentVariable(key, null);
            }

            base.Before(methodUnderTest);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            base.After(methodUnderTest);

            foreach (var key in _environmentVariables)
            {
                EnvironmentHelpers.SetEnvironmentVariable(key, null);
            }
        }
    }
}
