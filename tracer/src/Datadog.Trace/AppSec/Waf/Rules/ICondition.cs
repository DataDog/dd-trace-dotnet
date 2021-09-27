// <copyright file="ICondition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.AppSec.DataFormat;

namespace Datadog.Trace.AppSec.Waf.Rules
{
    internal interface ICondition
    {
        bool IsMatch(Node data);

        bool IsTransformedMatch(Node data, string transformation);
    }
}
