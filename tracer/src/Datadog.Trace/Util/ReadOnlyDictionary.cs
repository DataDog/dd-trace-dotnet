﻿// <copyright file="ReadOnlyDictionary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Util;

internal static class ReadOnlyDictionary
{
    public static readonly System.Collections.ObjectModel.ReadOnlyDictionary<string, string> Empty
        = new(new Dictionary<string, string>());
}
