// <copyright file="CustomSettingsForTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Security.Unit.Tests.Iast
{
    internal class CustomSettingsForTests : DictionaryObjectConfigurationSource
    {
        public CustomSettingsForTests(Dictionary<string, object> settings)
            : base(settings)
        {
        }
    }
}
