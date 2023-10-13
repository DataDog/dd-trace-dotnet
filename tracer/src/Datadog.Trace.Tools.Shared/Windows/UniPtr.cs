// <copyright file="UniPtr.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Original code from https://github.com/gapotchenko/Gapotchenko.FX/tree/master/Source/Gapotchenko.FX.Diagnostics.Process
// MIT License
//
// Copyright © 2019 Gapotchenko and Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1308 // Variable names should not be prefixed

using System;

namespace Datadog.Trace.Tools.Shared.Windows
{
    /// <summary>
    /// Universal pointer that can hold both 32 and 64 bit values.
    /// </summary>
    internal readonly struct UniPtr : IEquatable<UniPtr>
    {
        public UniPtr(IntPtr value)
        {
            m_Value = value.ToInt64();
            m_Size = (byte)IntPtr.Size;
        }

        public UniPtr(long value)
        {
            m_Value = value;
            m_Size = sizeof(long);
        }

        public UniPtr(ulong value)
            : this((long)value)
        {
        }

        private readonly long m_Value;
        private readonly byte m_Size;

        public int Size => m_Size;

        public readonly long ToInt64() => m_Value;

        public readonly ulong ToUInt64() => (ulong)m_Value;

        public readonly bool FitsInNativePointer => m_Size <= IntPtr.Size;

        public readonly bool CanBeRepresentedByNativePointer
        {
            get
            {
                int actualSize = m_Size;

                if (actualSize == 8)
                {
                    if (m_Value >> 32 == 0)
                    {
                        actualSize = 4;
                    }
                }

                return actualSize <= IntPtr.Size;
            }
        }

        public static implicit operator IntPtr(UniPtr p) => new IntPtr(p.ToInt64());

        public static implicit operator UniPtr(IntPtr p) => new UniPtr(p);

        public static UniPtr operator +(UniPtr a, long b) => new UniPtr(a.ToInt64() + b);

        public static bool operator ==(UniPtr a, UniPtr b) => a.ToUInt64() == b.ToUInt64();

        public static bool operator !=(UniPtr a, UniPtr b) => a.ToUInt64() != b.ToUInt64();

        public override int GetHashCode() => m_Value.GetHashCode();

        public override bool Equals(object? obj) => obj is UniPtr other && Equals(other);

        public bool Equals(UniPtr other) => m_Value == other.m_Value;

        public override readonly string ToString() => ((ulong)m_Value).ToString();
    }
}
