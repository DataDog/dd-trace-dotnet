// <copyright file="ProbeDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal abstract class ProbeDefinition : IEquatable<ProbeDefinition>
    {
        public string Language { get; set; }

        public string Id { get; set; }

        public long? OrgId { get; set; }

        public string AppId { get; set; }

        public bool Active { get; set; }

        public string[] Tags { get; set; }

        public Where Where { get; set; }

        public string[] AdditionalIds { get; set; }

        public int? Version { get; set; }

        public bool Equals(ProbeDefinition other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Language == other.Language && Id == other.Id && OrgId == other.OrgId && AppId == other.AppId && Active == other.Active && Equals(Where, other.Where) && Tags.NullableSequentialEquals(other.Tags) && Version == other.Version && AdditionalIds.NullableSequentialEquals(other.AdditionalIds);
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

            return Equals((ProbeDefinition)obj);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Language);
            hashCode.Add(Id);
            hashCode.Add(OrgId);
            hashCode.Add(AppId);
            hashCode.Add(Active);
            hashCode.Add(Tags);
            hashCode.Add(Where);
            hashCode.Add(AdditionalIds);
            hashCode.Add(Version);
            return hashCode.ToHashCode();
        }
    }
}
