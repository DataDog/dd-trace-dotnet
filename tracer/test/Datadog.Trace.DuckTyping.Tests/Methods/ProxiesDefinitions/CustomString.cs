// <copyright file="CustomString.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Microsoft.Extensions.Primitives;

namespace Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions
{
    public class CustomString : IEquatable<CustomString>
    {
        public CustomString(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static implicit operator string(CustomString customString)
        {
            return customString.Value;
        }

        public static implicit operator StringValues(CustomString customString)
        {
            return new StringValues(customString.Value);
        }

        public static implicit operator CustomString(string stringValue)
        {
            return new CustomString(stringValue);
        }

        public static implicit operator CustomString(StringValues stringValues)
        {
            return new CustomString(stringValues.ToString());
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is CustomString other && Equals(other);
        }

        public bool Equals(CustomString other)
        {
            return Value == other.Value;
        }
    }
}
