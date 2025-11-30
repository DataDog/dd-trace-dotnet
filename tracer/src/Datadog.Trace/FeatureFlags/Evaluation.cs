// <copyright file="Evaluation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Datadog.Trace.FeatureFlags
{
    internal class Evaluation<T>(T value, EvaluationReason reason, string? variant = null, string? error = null, Dictionary<string, string>? metadata = null)
    {
        public T Value { get; } = value;

        public EvaluationReason Reason { get; } = reason;

        public string? Variant { get; } = variant;

        public string? Error { get; } = error;

        public Dictionary<string, string>? Metadata { get; } = metadata;
    }
}
