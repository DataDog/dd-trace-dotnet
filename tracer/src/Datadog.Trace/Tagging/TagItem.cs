// <copyright file="TagItem.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tagging
{
    internal readonly ref struct TagItem<T>
    {
        public readonly string Key;
        public readonly T Value;
        public readonly byte[] KeyUtf8;

        public TagItem(string key, T value, byte[] keyUtf8)
        {
            Key = key;
            Value = value;
            KeyUtf8 = keyUtf8;
        }
    }
}
