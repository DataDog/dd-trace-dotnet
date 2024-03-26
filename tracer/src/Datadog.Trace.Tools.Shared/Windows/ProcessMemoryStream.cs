// <copyright file="ProcessMemoryStream.cs" company="Datadog">
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

using System;
using System.IO;

namespace Datadog.Trace.Tools.Shared.Windows
{
    internal sealed class ProcessMemoryStream : Stream
    {
        private readonly IProcessMemoryAdapter _adapter;
        private readonly UniPtr _baseAddress;
        private readonly long _regionLength;

        private readonly uint _pageSize;
        private readonly UniPtr _firstPageAddress;

        private long _position;

        public ProcessMemoryStream(IProcessMemoryAdapter adapter, UniPtr baseAddress, long regionLength)
        {
            _adapter = adapter;
            _baseAddress = baseAddress;
            _regionLength = regionLength;

            _pageSize = (uint)adapter.PageSize;
            _firstPageAddress = GetPageLowerBound(baseAddress);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                if (_regionLength == -1)
                {
                    throw new NotSupportedException();
                }

                return _regionLength;
            }
        }

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _position = value;
            }
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        private UniPtr GetPageLowerBound(UniPtr address)
        {
            var pageSize = _pageSize;
            return new UniPtr(address.ToUInt64() / pageSize * pageSize);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset + count > buffer.Length)
            {
                throw new ArgumentException();
            }

            int totalCount = 0;
            do
            {
                var addr = _baseAddress + _position;

                var pageStart = GetPageLowerBound(addr);
                var pageEnd = pageStart + _pageSize;

                int currentCount = count;

                var remainingPageSize = pageEnd.ToUInt64() - addr.ToUInt64();
                if ((ulong)currentCount > remainingPageSize)
                {
                    currentCount = (int)remainingPageSize;
                }

                var regionLength = _regionLength;
                if (regionLength != -1)
                {
                    long remainingRegionLength = regionLength - _position;
                    if (currentCount > remainingRegionLength)
                    {
                        currentCount = (int)remainingRegionLength;
                    }
                }

                if (currentCount == 0)
                {
                    // EOF
                    return totalCount;
                }

                bool throwOnError = pageStart == _firstPageAddress;

                int r = _adapter.ReadMemory(addr, buffer, offset, currentCount, throwOnError);
                if (r <= 0)
                {
                    if (throwOnError)
                    {
                        // In case if process memory adapter disregards a throw on error flag.
                        throw new IOException();
                    }

                    // Assume EOF.
                    break;
                }

                count -= r;
                offset += r;
                totalCount += r;

                _position += r;
            }
            while (count > 0);

            return totalCount;
        }

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
