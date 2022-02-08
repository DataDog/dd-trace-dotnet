// <copyright file="LocationDescriptor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.PProf.Export
{
    internal struct LocationDescriptor : IEquatable<LocationDescriptor>
    {
        private readonly byte _locationKind;
        private readonly ulong _locationInfoCode;

        public LocationDescriptor(byte locationKind, ulong locationInfoCode)
        {
            _locationKind = locationKind;
            _locationInfoCode = locationInfoCode;
        }

        public byte LocationKind
        {
            get { return _locationKind; }
        }

        public ulong LocationInfoCode
        {
            get { return _locationInfoCode; }
        }

        public static bool operator ==(LocationDescriptor one, LocationDescriptor anoter)
        {
            return one.Equals(anoter);
        }

        public static bool operator !=(LocationDescriptor one, LocationDescriptor anoter)
        {
            return !(one == anoter);
        }

        public override int GetHashCode()
        {
            return (int)(
                (((uint)_locationKind) << 24)
                ^ ((uint)_locationInfoCode)
                ^ ((uint)(_locationInfoCode >> 32)));
        }

        bool IEquatable<LocationDescriptor>.Equals(LocationDescriptor other)
        {
            return this.Equals(other);
        }

        public override bool Equals(object obj)
        {
            if (obj != null && obj is LocationDescriptor other)
            {
                return this.Equals(other);
            }

            return false;
        }

        public bool Equals(LocationDescriptor other)
        {
            return (_locationKind == other._locationKind) && (_locationInfoCode == other._locationInfoCode);
        }
    }
}