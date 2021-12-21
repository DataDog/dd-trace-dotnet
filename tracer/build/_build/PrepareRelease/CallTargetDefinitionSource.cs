// <copyright file="IntegrationGroups.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace PrepareRelease
{
    public class CallTargetDefinitionSource
    {
        public string IntegrationName { get; init; }

        public string TargetAssembly { get; init; }

        public string TargetType { get; init; }

        public string TargetMethod { get; init; }

        public string[] TargetSignatureTypes { get; init; }

        public ushort TargetMinimumMajor { get; init; }

        public ushort TargetMinimumMinor { get; init; }

        public ushort TargetMinimumPatch { get; init; }

        public ushort TargetMaximumMajor { get; init; }

        public ushort TargetMaximumMinor { get; init; }

        public ushort TargetMaximumPatch { get; init; }

        public string WrapperAssembly { get; init; }

        public string WrapperType { get; init; }

        public IntegrationType IntegrationType { get; init; }

        protected bool Equals(CallTargetDefinitionSource other) =>
            IntegrationName == other.IntegrationName &&
            TargetAssembly == other.TargetAssembly &&
            TargetType == other.TargetType &&
            TargetMethod == other.TargetMethod &&
            TargetSignatureTypes?.Length == other.TargetSignatureTypes?.Length &&
            TargetMinimumMajor == other.TargetMinimumMajor &&
            TargetMinimumMinor == other.TargetMinimumMinor &&
            TargetMinimumPatch == other.TargetMinimumPatch &&
            TargetMaximumMajor == other.TargetMaximumMajor &&
            TargetMaximumMinor == other.TargetMaximumMinor &&
            TargetMaximumPatch == other.TargetMaximumPatch &&
            WrapperAssembly == other.WrapperAssembly &&
            WrapperType == other.WrapperType &&
            IntegrationType == other.IntegrationType &&
            string.Join(',', TargetSignatureTypes ?? Array.Empty<string>()) == string.Join(',', other.TargetSignatureTypes ?? Array.Empty<string>());

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

            return Equals((CallTargetDefinitionSource)obj);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(IntegrationName);
            hash.Add(TargetAssembly);
            hash.Add(TargetType);
            hash.Add(TargetMethod);
            hash.Add(TargetMinimumMajor);
            hash.Add(TargetMinimumMinor);
            hash.Add(TargetMinimumPatch);
            hash.Add(TargetMaximumMajor);
            hash.Add(TargetMaximumMinor);
            hash.Add(TargetMaximumPatch);
            hash.Add(WrapperAssembly);
            hash.Add(WrapperType);
            hash.Add(IntegrationType);
            return hash.ToHashCode();
        }
    }
}
