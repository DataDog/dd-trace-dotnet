// <copyright file="ConventionalRoutesController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.AspNetCore.Mvc;

namespace Samples.AspNetCoreNetFramework
{
    public class ConventionalRoutesController : ControllerBase
    {
        public IActionResult Index() => Ok("conventional-route");
    }
}
