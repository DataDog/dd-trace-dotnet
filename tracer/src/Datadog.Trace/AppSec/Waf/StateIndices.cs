// <copyright file="StateIndices.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Numerics;

namespace Datadog.Trace.AppSec.Waf
{
    internal static class StateIndices
    {
        public static readonly BigInteger AppsecCanBeSwitched = Create(0);

        private static BigInteger Create(int index) => new(1UL << index);
    }
}
