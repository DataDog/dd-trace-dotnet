// <copyright file="ProfileValueRead.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    /// <summary>
    /// One sample-type lookup. Unknown, missing and a present zero stay distinct.
    /// </summary>
    internal readonly struct ProfileValueRead
    {
        private const byte UnknownKind = 0;
        private const byte MissingKind = 1;
        private const byte PresentKind = 2;

        private readonly byte _kind;
        private readonly long _value;

        private ProfileValueRead(byte kind, long value)
        {
            _kind = kind;
            _value = value;
        }

        public static ProfileValueRead Unknown { get; } = new ProfileValueRead(UnknownKind, 0);

        public static ProfileValueRead Missing { get; } = new ProfileValueRead(MissingKind, 0);

        public bool IsUnknown => _kind == UnknownKind;

        public bool IsMissing => _kind == MissingKind;

        public bool IsPresent => _kind == PresentKind;

        public bool IsZero => _kind == PresentKind && _value == 0;

        public long Value
        {
            get
            {
                if (_kind != PresentKind)
                {
                    throw new InvalidOperationException(
                        _kind == MissingKind
                            ? "The profile value is missing."
                            : "The profile value type is unknown.");
                }

                return _value;
            }
        }

        public static ProfileValueRead Present(long value) => new ProfileValueRead(PresentKind, value);
    }
}
