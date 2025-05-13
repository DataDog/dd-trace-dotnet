// <copyright file="EnvironmentRestorerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Xunit;

[AttributeUsage(AttributeTargets.Class)]
public class EnvironmentRestorerAttribute : BeforeAfterTestAttribute
{
    private readonly string[] _variables;
    private readonly Dictionary<string, string> _originalVariables;

    public EnvironmentRestorerAttribute(params string[] args)
    {
        _variables = args;
        _originalVariables = new(args.Length);
    }

    public override void Before(MethodInfo methodUnderTest)
    {
        foreach (var variable in _variables)
        {
            _originalVariables[variable] = Environment.GetEnvironmentVariable(variable);
            // clear variable
            Environment.SetEnvironmentVariable(variable, null);
        }

        base.Before(methodUnderTest);
    }

    public override void After(MethodInfo methodUnderTest)
    {
        foreach (var variable in _originalVariables)
        {
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }
    }
}
