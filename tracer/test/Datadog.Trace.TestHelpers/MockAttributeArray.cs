// <copyright file="MockAttributeArray.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using MessagePack;

namespace Datadog.Trace.TestHelpers
{
    [MessagePackObject]
    public class MockAttributeArray
    {
        [Key("values")]
        public List<MockAttributeArrayValue> Values { get; set; }
    }
}
