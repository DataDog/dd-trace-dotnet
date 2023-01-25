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
        internal ProbeDefinition()
        {
            EvaluateAt = EvaluateAt.Exit;
        }

        public string Language { get; set; }

        public string Id { get; set; }

        public string[] Tags { get; set; }

        public Where Where { get; set; }

        public EvaluateAt EvaluateAt { get; set; }

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

            return Language == other.Language && Id == other.Id && Equals(Where, other.Where) && Equals(EvaluateAt, other.EvaluateAt) && Tags.NullableSequentialEquals(other.Tags) && Version == other.Version && AdditionalIds.NullableSequentialEquals(other.AdditionalIds);
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
            hashCode.Add(Tags);
            hashCode.Add(Where);
            hashCode.Add(EvaluateAt);
            hashCode.Add(AdditionalIds);
            hashCode.Add(Version);
            return hashCode.ToHashCode();
        }
    }
}
