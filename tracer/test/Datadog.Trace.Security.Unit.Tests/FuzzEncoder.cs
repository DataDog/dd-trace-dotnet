// <copyright file="FuzzEncoder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using Encoder = Datadog.Trace.AppSec.WafEncoding.Encoder;

namespace Datadog.Trace.Security.Unit.Tests;

public class FuzzEncoder : WafLibraryRequiredTest
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly Encoder _encoder;
    private readonly EncoderLegacy _encoderLegacy;

    public FuzzEncoder(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _encoder = new Encoder();
        _encoderLegacy = new EncoderLegacy(WafLibraryInvoker!);
    }

    [Fact]
    public void LetsFuzz()
    {
        // if we don't throw any exceptions and generate a valid object the the test is successful

        var jsonGenerator = new JsonGenerator();

        var errorOccured = false;

        for (var i = 0; i < 100; i++)
        {
            var buffer = jsonGenerator.GenerateJsonBuffer();
            try
            {
                using var memoryStream = new MemoryStream(buffer.Array!, buffer.Offset, buffer.Count, false);
                using var streamReader = new StreamReader(memoryStream);
                using var jsonReader = new JsonTextReader(streamReader);
                var root = JToken.ReadFrom(jsonReader);

                using var result = _encoder.Encode(root, applySafetyLimits: true);

                // check the object is valid
                Assert.NotEqual(DDWAF_OBJ_TYPE.DDWAF_OBJ_INVALID, result.ResultDdwafObject.Type);

                using var resultObj = _encoderLegacy.Encode(root, applySafetyLimits: true);

                Assert.NotEqual(DDWAF_OBJ_TYPE.DDWAF_OBJ_INVALID, result.ResultDdwafObject.Type);
            }
            catch (Exception ex)
            {
                errorOccured = true;

                _outputHelper.WriteLine($"Error occured on run '{i}' parsing json: {ex}");
                _outputHelper.WriteLine("Json causing the error was:");
                ViewJson(buffer);
            }
        }

        Assert.False(errorOccured);
    }

    private void ViewJson(ArraySegment<byte> buffer)
    {
        var jsonText = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
        _outputHelper.WriteLine(jsonText);
    }
}
