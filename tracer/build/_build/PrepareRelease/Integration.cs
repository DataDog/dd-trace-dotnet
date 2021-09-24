// <copyright file="Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace PrepareRelease
{
    public class Integration
    {
        public string Name { get; init; }

        public MethodReplacement[] MethodReplacements { get; init; }

        public class MethodReplacement
        {
            public CallerDetail Caller { get; init; }

            public TargetDetail Target { get; init; }

            public WrapperDetail Wrapper { get; init; }

            protected bool Equals(MethodReplacement other) =>
                Equals(Caller, other.Caller) &&
                Equals(Target, other.Target) &&
                Equals(Wrapper, other.Wrapper);

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

                return Equals((MethodReplacement)obj);
            }

            public override int GetHashCode() => HashCode.Combine(Caller, Target, Wrapper);
        }

        public record CallerDetail
        {
            public string Assembly { get; init; }

            public string Type { get; init; }

            public string Method { get; init; }

        }

        public class TargetDetail
        {
            public string Assembly { get; init; }

            public string Type { get; init; }

            public string Method { get; init; }

            public string Signature { get; init; }

            public string[] SignatureTypes { get; init; }

            public ushort MinimumMajor { get; init; }

            public ushort MinimumMinor { get; init; }

            public ushort MinimumPatch { get; init; }

            public ushort MaximumMajor { get; init; }

            public ushort MaximumMinor { get; init; }

            public ushort MaximumPatch { get; init; }

            private bool Equals(TargetDetail other) =>
                Assembly == other.Assembly &&
                Type == other.Type &&
                Method == other.Method &&
                Signature == other.Signature &&
                ((SignatureTypes is null && other.SignatureTypes is null) ||
                 (SignatureTypes is not null && other.SignatureTypes is not null &&
                  string.Join(",", SignatureTypes) == string.Join(",", other.SignatureTypes))) &&
                MinimumMajor == other.MinimumMajor &&
                MinimumMinor == other.MinimumMinor &&
                MinimumPatch == other.MinimumPatch &&
                MaximumMajor == other.MaximumMajor &&
                MaximumMinor == other.MaximumMinor &&
                MaximumPatch == other.MaximumPatch;

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

                return Equals((TargetDetail)obj);
            }

            public override int GetHashCode()
            {
                var hashCode = new HashCode();
                hashCode.Add(Assembly);
                hashCode.Add(Type);
                hashCode.Add(Method);
                hashCode.Add(Signature);
                hashCode.Add(SignatureTypes?.Length > 0 ? string.Join(",", SignatureTypes) : null);
                hashCode.Add(MinimumMajor);
                hashCode.Add(MinimumMinor);
                hashCode.Add(MinimumPatch);
                hashCode.Add(MaximumMajor);
                hashCode.Add(MaximumMinor);
                hashCode.Add(MaximumPatch);
                return hashCode.ToHashCode();
            }
        }

        public record WrapperDetail
        {
            public string Assembly { get; init; }

            public string Type { get; init; }

            public string Method { get; init; }

            public string Signature { get; init; }

            public string Action { get; init; }
        }
    }
}