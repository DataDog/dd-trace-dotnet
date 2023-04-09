// <copyright file="Constants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.SourceGenerators.AspectsDefinitions;

internal static class Constants
{
    public const string AspectClassAttributeName = "AspectClassAttribute";
    public const string AspectClassAttributeFullName = "Datadog.Trace.Iast.Dataflow." + AspectClassAttributeName;
}
