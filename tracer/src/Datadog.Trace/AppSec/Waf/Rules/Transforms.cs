// <copyright file="Transforms.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec.DataFormat;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf.Rules
{
    internal static class Transforms
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Transforms));

        public static Node Transform(Node data, string transform)
        {
            switch (transform)
            {
                case "lowercase":
                    return Visitor.Map(data, x => Node.NewString(x.StringValue.ToLowerInvariant()));

                case "removeNulls":
                    return Visitor.Map(data, x => Node.NewString(x.StringValue.Replace("\0", string.Empty)));

                default:
                    Log.Error("Unknown transform: {Transform}", transform);
                    throw new Exception($"Unknown transform: {transform}");
            }
        }
    }
}
