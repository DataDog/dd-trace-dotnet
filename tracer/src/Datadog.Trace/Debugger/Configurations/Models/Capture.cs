// <copyright file="Capture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal struct Capture : IEquatable<Capture>
    {
        public int MaxReferenceDepth { get; set; }

        public int MaxCollectionSize { get; set; }

        public int MaxLength { get; set; }

        public int MaxFieldCount { get; set; }

        public bool Equals(Capture other)
        {
            return MaxReferenceDepth == other.MaxReferenceDepth && MaxCollectionSize == other.MaxCollectionSize && MaxLength == other.MaxLength && MaxFieldCount == other.MaxFieldCount;
        }

        public override bool Equals(object obj)
        {
            return obj is Capture other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MaxReferenceDepth, MaxCollectionSize, MaxLength, MaxFieldCount);
        }
    }
}
