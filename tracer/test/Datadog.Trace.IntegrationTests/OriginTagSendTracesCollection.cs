// <copyright file="OriginTagSendTracesCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    [CollectionDefinition(nameof(OriginTagSendTracesCollection), DisableParallelization = true)]
    public class OriginTagSendTracesCollection
    {
    }
}
