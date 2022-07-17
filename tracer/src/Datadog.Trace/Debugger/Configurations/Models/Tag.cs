// <copyright file="Tag.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal record Tag
    {
        public string Key { get; set; }

        public string Value { get; set; }

        public override string ToString()
        {
            return $"{Key}:{Value}";
        }

        public static Tag FromString(string str)
        {
            var index = str?.IndexOf(':');
            if (index is null or -1)
            {
                return null;
            }

            var key = str.Substring(0, index.Value + 1);
            var value = str.Substring(index.Value + 1);

            return new Tag() { Key = key, Value = value };
        }
    }
}
