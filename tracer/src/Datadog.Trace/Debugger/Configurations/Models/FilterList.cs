// <copyright file="FilterList.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal class FilterList : IEquatable<FilterList>
    {
        public string[] PackagePrefixes { get; set; }

        public string[] Classes { get; set; }

        public bool Equals(FilterList other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return PackagePrefixes.NullableSequentialEquals(other.PackagePrefixes) && Classes.NullableSequentialEquals(other.Classes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((FilterList)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PackagePrefixes, Classes);
        }
    }
}
