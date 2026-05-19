// <copyright file="SymbolUploadApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests;

public class SymbolUploadApiTests
{
    [Fact]
    public void EventMetadata_IsValidJson_AndContainsAllFields()
    {
        // Include quotes to ensure proper JSON escaping/quoting
        const string serviceName = "test\"service";
        const string version = "1.0.0";
        const string runtimeId = "runtime-id";
        var uploadId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        const long batchNum = 7;
        const int attachmentSize = 12345;

        var metadata = new SymDbUploadMetadata(
            Service: serviceName,
            Version: version,
            UploadId: uploadId,
            BatchNum: batchNum,
            Final: false);
        var bytes = SymbolUploadApi.CreateEventMetadata(metadata, runtimeId, attachmentSize);
        var json = Encoding.UTF8.GetString(bytes.Array!, bytes.Offset, bytes.Count);

        var jobj = JObject.Parse(json);

        Assert.Equal("dd_debugger", (string?)jobj["ddsource"]);
        Assert.Equal(serviceName, (string?)jobj["service"]);
        Assert.Equal(version, (string?)jobj["version"]);
        Assert.Equal("dotnet", (string?)jobj["language"]);
        Assert.Equal(runtimeId, (string?)jobj["runtimeId"]);
        Assert.Equal("symdb", (string?)jobj["type"]);
        Assert.Equal(uploadId.ToString(), (string?)jobj["uploadId"]);
        Assert.Equal(batchNum, (long?)jobj["batchNum"]);
        Assert.Equal(false, (bool?)jobj["final"]);
        Assert.Equal(attachmentSize, (int?)jobj["attachmentSize"]);
    }
}
