// <copyright file="IntegrationGroups.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;

namespace PrepareRelease
{
    public class IntegrationGroups
    {
        public List<Integration> CallSite { get; set; }
        public List<Integration> CallTarget { get; set; }
        public List<Integration> All => CallSite.Concat(CallTarget).OrderBy(i => i.Name).ToList();
    }
}