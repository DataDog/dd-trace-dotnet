// <copyright file="AreaRoutesController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.AspNetCore.Mvc;

namespace Samples.AspNetCoreNetFramework
{
    [Area("Admin")]
    public class AreaRoutesController : ControllerBase
    {
        public IActionResult Index() => Ok("area-route");
    }
}
