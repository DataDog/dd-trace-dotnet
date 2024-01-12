// <copyright file="SymDbEnablement.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymDbEnablement
    {
        [JsonProperty("upload_symbols")]
        public bool UploadSymbols { get; set; }
    }
}
