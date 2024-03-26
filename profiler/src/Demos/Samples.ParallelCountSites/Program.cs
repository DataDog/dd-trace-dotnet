// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Samples.ParallelCountSites
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            await Console.Out.WriteLineAsync($"pid = {Process.GetCurrentProcess().Id}");
            await Console.Out.WriteLineAsync();

            int iterations = 100;
            var downloader = new Downloader();
            await downloader.SumPagesSize(iterations);
        }
    }
}
