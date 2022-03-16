// <copyright file="FuzzEncoder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class FuzzEncoder
    {
        private readonly ITestOutputHelper _outputHelper;

        public FuzzEncoder(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public void LetsFuzz()
        {
            // if we don't throw any exceptions and generate a valid object the the test is successful

            var libraryHandle = LibraryLoader.LoadAndGetHandle();
            var wafNative = new WafNative(libraryHandle);
            var encoder = new AppSec.Waf.Encoder(wafNative);

            var jsonGenerator = new JsonGenerator();

            var errorOccured = false;

            for (int i = 0; i < 100; i++)
            {
                var buffer = jsonGenerator.GenerateJsonBuffer();
                try
                {
                    using var memoryStream = new MemoryStream(buffer.Array, buffer.Offset, buffer.Count, false);
                    using var streamReader = new StreamReader(memoryStream);
                    using var jsonReader = new JsonTextReader(streamReader);
                    var root = JToken.ReadFrom(jsonReader);

                    var l = new List<Obj>();
                    using var result = encoder.Encode(root, l, applySafetyLimits: true);

                    // check the object is valid
                    Assert.NotEqual(ObjType.Invalid, result.ArgsType);

                    l.ForEach(x => x.Dispose());
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
            var jsonText = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
            _outputHelper.WriteLine(jsonText);
        }
    }
}
