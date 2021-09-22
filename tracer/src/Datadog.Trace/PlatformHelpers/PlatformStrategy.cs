// <copyright file="PlatformStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.PlatformHelpers
{
    internal static class PlatformStrategy
    {
        private static Func<Scope, bool> _shouldSkipClientSpan = (s) => false;

        public static Func<Scope, bool> ShouldSkipClientSpan
        {
            get { return _shouldSkipClientSpan; }
            set { _shouldSkipClientSpan = value; }
        }
    }
}
