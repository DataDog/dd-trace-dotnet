// <copyright file="SymbolUploadApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests;

public class SymbolUploadApiTests
{
    [Fact]
    public void EventMetadata_IsValidJson_AndDebuggerTypeIsQuoted()
    {
        // Include quotes to ensure proper JSON escaping/quoting
        const string serviceName = "test\"service";
        const string runtimeId = "runtime-id";

        var bytes = SymbolUploadApi.CreateEventMetadata(serviceName, runtimeId);
        var json = Encoding.UTF8.GetString(bytes.Array!, bytes.Offset, bytes.Count);

        var dict = JsonConvert.DeserializeObject<Dictionary<string, object?>>(json);

        Assert.NotNull(dict);
        Assert.Equal("dd_debugger", dict!["ddsource"]);
        Assert.Equal(serviceName, dict["service"]);
        Assert.Equal(runtimeId, dict["runtimeId"]);
        Assert.Equal("symdb", dict["debugger.type"]);
    }
}
