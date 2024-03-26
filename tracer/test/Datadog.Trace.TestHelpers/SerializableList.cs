// <copyright file="SerializableList.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers;

public class SerializableList<T> : IXunitSerializable, IEnumerable<T>
{
    public List<T> Values { get; private set; } = new();

    public void Add(T value) => Values.Add(value);

    public IEnumerator<T> GetEnumerator() => Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Values.GetEnumerator();

    public void Deserialize(IXunitSerializationInfo info)
    {
        Values = JsonConvert.DeserializeObject<List<T>>(info.GetValue<string>(nameof(Values)));
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(Values), JsonConvert.SerializeObject(Values));
    }
}
