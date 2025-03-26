// <copyright file="MockSpanEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using MessagePack;

namespace Datadog.Trace.TestHelpers
{
    [MessagePackObject]
    public class MockSpanEvent
    {
        [Key("name")]
        public string Name { get; set; }

        [Key("time_unix_nano")]
        public long Timestamp { get; set; }

        [Key("attributes")]
        public Dictionary<string, MockAttributeAnyValue> Attributes { get; set; }
    }
}
