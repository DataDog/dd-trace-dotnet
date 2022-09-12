// <copyright file="SecurityTransparentTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Security;

// [assembly: SecurityTransparent]

namespace Samples.Probes.Contracts.Security
{
    /// <summary>
    /// For Tests
    /// </summary>
    public class SecurityTransparentTest
    {
        private readonly Home _home = new Home { Name = "Harry House" };

        internal class Home
        {
            public string Name { get; set; }
        }
    }
}
