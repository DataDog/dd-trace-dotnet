// <copyright file="StackSnapshotsBufferSegmentCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

using Datadog.Collections;
using Datadog.Util;

namespace Datadog.Profiler
{
    internal class StackSnapshotsBufferSegmentCollection : IDisposable
    {
        private const int SegmentsListAllocationStep = 128;
        private readonly GrowingCollection<StackSnapshotsBufferSegment> _segments;

        private readonly object _updateLock = new object();

        private bool _isReadonly;
        private ulong _totalByteCount;
        private ulong _totalSnapshotsCount;
        private DateTimeOffset _totalTimeRangeStart;
        private DateTimeOffset _totalTimeRangeEnd;

        public StackSnapshotsBufferSegmentCollection()
        {
            _segments = new GrowingCollection<StackSnapshotsBufferSegment>(SegmentsListAllocationStep);
            _isReadonly = false;
            _totalByteCount = 0;
            _totalSnapshotsCount = 0;
            _totalTimeRangeStart = DateTimeOffset.MinValue;
            _totalTimeRangeEnd = DateTimeOffset.MinValue;
        }

        ~StackSnapshotsBufferSegmentCollection()
        {
            ReleaseAllSegments();
        }

        public bool IsReadonly
        {
            get { return _isReadonly; }
        }

        public IReadOnlyCollection<StackSnapshotsBufferSegment> Segments
        {
            get { return _segments; }
        }

        public ulong TotalByteCount
        {
            get { return _totalByteCount; }
        }

        public ulong TotalSnapshotsCount
        {
            get { return _totalSnapshotsCount; }
        }

        public DateTimeOffset TotalTimeRangeStart
        {
            get { return _totalTimeRangeStart; }
        }

        public DateTimeOffset TotalTimeRangeEnd
        {
            get { return _totalTimeRangeEnd; }
        }

        public void Dispose()
        {
            if (ReleaseAllSegments())
            {
                GC.SuppressFinalize(this);
            }
        }

        public void GetTotalsSafe(
                        out bool isReadonly,
                        out ulong totalByteCount,
                        out ulong totalSnapshotsCount,
                        out DateTimeOffset totalTimeRangeStart,
                        out DateTimeOffset totalTimeRangeEnd)
        {
            lock (_updateLock)
            {
                isReadonly = _isReadonly;
                totalByteCount = _totalByteCount;
                totalSnapshotsCount = _totalSnapshotsCount;
                totalTimeRangeStart = _totalTimeRangeStart;
                totalTimeRangeEnd = _totalTimeRangeEnd;
            }
        }

        public void MakeReadonly()
        {
            if (!_isReadonly)
            {
                lock (_updateLock)
                {
                    _isReadonly = true;
                }
            }
        }

        public bool Add(StackSnapshotsBufferSegment segment)
        {
            if (_isReadonly)
            {
                return false;
            }

            Validate.NotNull(segment, nameof(segment));

            lock (_updateLock)
            {
                if (_isReadonly)
                {
                    return false;
                }

                if (_segments.Count == 0)
                {
                    _totalTimeRangeStart = segment.TimeRangeStart;
                    _totalTimeRangeEnd = segment.TimeRangeEnd;
                }
                else
                {
                    if (segment.TimeRangeStart < _totalTimeRangeStart)
                    {
                        _totalTimeRangeStart = segment.TimeRangeStart;
                    }

                    if (segment.TimeRangeEnd > _totalTimeRangeEnd)
                    {
                        _totalTimeRangeEnd = segment.TimeRangeEnd;
                    }
                }

                _totalByteCount += segment.SegmentByteCount;
                _totalSnapshotsCount += segment.SnapshotsCount;
                _segments.Add(segment);

                return true;
            }
        }

        private bool ReleaseAllSegments()
        {
            MakeReadonly();

            bool allDisposed = true;
            foreach (StackSnapshotsBufferSegment segment in _segments)
            {
                segment.Dispose();
                allDisposed = allDisposed && segment.IsDisposed();
            }

            return allDisposed;
        }
    }
}