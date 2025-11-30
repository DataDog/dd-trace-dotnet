// <copyright file="EvaluationReason.cs" company="Datadog">
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
    internal enum EvaluationReason
    {
        DEFAULT,
        STATIC,
        TARGETING_MATCH,
        SPLIT,
        DISABLED,
        CACHED,
        UNKNOWN,
        ERROR
    }
}
