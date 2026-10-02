// <copyright file="SteadyStateController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace BuggyBits.Controllers
{
    [Route("SteadyState")]
    public class SteadyStateController : Controller
    {
        private const int Blocks = 64;
        private const int BlockSize = 4 * 1024; // 4 KB (< 85 KB => stays off the LOH)

        [HttpGet]
        public IActionResult Get()
        {
            // Deterministic, bounded, short-lived allocations: same ~256 KB Gen0 every
            // request, immediately collectable => heap reaches a stable plateau.
            var sink = new List<byte[]>(Blocks);
            for (int i = 0; i < Blocks; i++)
            {
                var block = new byte[BlockSize];
                block[0] = (byte)i; // touch so the page commits
                sink.Add(block);
            }

            return Content($"steady-state ok: {sink.Count} blocks", "text/plain");
        }
    }
}
