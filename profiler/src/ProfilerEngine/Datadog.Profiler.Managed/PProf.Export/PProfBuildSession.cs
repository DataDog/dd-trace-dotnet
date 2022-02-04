// <copyright file="PProfBuildSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Datadog.Util;
using PProfProto = Perftools.Profiles;

namespace Datadog.PProf.Export
{
    internal sealed class PProfBuildSession : IDisposable
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // The _profile field will contain collections of all its data.
        // This is a duplication of the underlying session lists contained in the PProfBuilder (but not the duplication of the actual data).
        // Moreover, this is a "bad" duplication since the _profile is discarded and
        // the underlying array will need to be re-allocated for the next session.
        // We should review this in the future, as we consider our own serializer.
        // Clearing the _profile collections wont help, because they are releasing the pre-allocated array,
        // so that it will need to be re-allocated again if it grows later:
        // https://github.com/protocolbuffers/protobuf/blob/59cc6d01ac0916258e68359f6f062532a85966ae/csharp/src/Google.Protobuf/Collections/RepeatedField.cs#L245-L249
        private PProfProto.Profile _profile;
        private PProfBuildSessionState _ownerBuildState = null;
        private long _timestampNanosecs = ProtoConstants.NumericValue.UnsetInt64;
        private long _durationNanosecs = ProtoConstants.NumericValue.UnsetInt64;
        private long _period = ProtoConstants.NumericValue.UnsetInt64;
        private PProfInfo.String _comment = null;
        private PProfProto.Sample _lastSample;

        internal PProfBuildSession(PProfBuilder owner, PProfBuildSessionState ownerBuildState)
        {
            Validate.NotNull(owner, nameof(owner));
            Validate.NotNull(ownerBuildState, nameof(ownerBuildState));

            _ownerBuildState = ownerBuildState;

            this.OwnerBuilder = owner;
            _profile = new PProfProto.Profile();
            _lastSample = null;
            this.SamplesCount = 0;
        }

        public long TimestampNanosecs
        {
            get
            {
                return _timestampNanosecs;
            }

            set
            {
                _timestampNanosecs = value;
                _profile.TimeNanos = _timestampNanosecs;
            }
        }

        public DateTimeOffset Timestamp
        {
            get
            {
                return Epoch + TimeSpan.FromMilliseconds(_timestampNanosecs / 1000000.0);
            }

            set
            {
                TimeSpan delta = value - Epoch;
                TimestampNanosecs = (long)(delta.TotalMilliseconds * 1000000.0);
            }
        }

        public long DurationNanosecs
        {
            get
            {
                return _durationNanosecs;
            }

            set
            {
                _durationNanosecs = value;
                _profile.DurationNanos = _durationNanosecs;
            }
        }

        public TimeSpan Duration
        {
            get
            {
                return TimeSpan.FromMilliseconds(_durationNanosecs / 1000000.0);
            }

            set
            {
                DurationNanosecs = (long)(value.TotalMilliseconds * 1000000.0);
            }
        }

        public long Period
        {
            get
            {
                return _period;
            }

            set
            {
                _period = value;
                _profile.Period = _period;
            }
        }

        public string Comment
        {
            get
            {
                return _comment?.Item;
            }
        }

        public PProfBuilder OwnerBuilder { get; }

        public ulong SamplesCount { get; private set; }

        private PProfBuildSessionState ValidOwnerBuildState
        {
            get
            {
                PProfBuildSessionState ownerBuildState = _ownerBuildState;
                if (ownerBuildState == null)
                {
                    throw new ObjectDisposedException(nameof(PProfBuildSession), $"{nameof(PProfBuildSession)} is already disposed.");
                }

                if (!ReferenceEquals(this, ownerBuildState.Session))
                {
                    // this might happen if the session has been disposed
                    throw new InvalidOperationException(
                                $"The _buildState of the {nameof(PProfBuilder)} " +
                                $"that owns this {nameof(PProfBuildSession)} does not refer to this session.");
                }

                return ownerBuildState;
            }
        }

        public void Dispose()
        {
            PProfBuildSessionState prevOwnerBuildState = Interlocked.Exchange(ref _ownerBuildState, null);

            if (prevOwnerBuildState != null)
            {
                if (!OwnerBuilder.TryFinishPProfBuildSessionInternal(this))
                {
                    throw new InvalidOperationException($"Cannot dispose this {nameof(PProfBuildSession)}."
                                                      + $" Is this not the current session of the {nameof(OwnerBuilder)}?");
                }
            }
        }

        public void WriteProfileToStream(Stream outputStream)
        {
            Validate.NotNull(outputStream, nameof(outputStream));
            Google.Protobuf.MessageExtensions.WriteTo(_profile, outputStream);
        }

        public void AddNextSample()
        {
            _lastSample = new PProfProto.Sample();
            _lastSample.Value.AddRange(OwnerBuilder.SampleValuesUnset);

            _profile.Sample.Add(_lastSample);
            SamplesCount = SamplesCount + 1;
        }

        public void SetSampleValues(params long[] values)
        {
            SetSampleValues((IEnumerable<long>)values);
        }

        public void SetSampleValues(IEnumerable<long> values)
        {
            if (SamplesCount == 0)
            {
                throw new InvalidOperationException($"Cannot invoke {nameof(SetSampleValues)}(..) because"
                                                  + $" no samples were added to the current {nameof(PProfBuildSession)}.");
            }

            int valueCount = values?.Count() ?? 0;
            if (valueCount != OwnerBuilder.SampleValueTypes.Count)
            {
                throw new ArgumentException($"The count of elements in the specified {nameof(values)}-enumerable (={valueCount})"
                                          + $" must be the equal to the number of configured {nameof(OwnerBuilder.SampleValueTypes)}"
                                          + $" (={OwnerBuilder.SampleValueTypes.Count}).");
            }

            _lastSample.Value.Clear();
            _lastSample.Value.AddRange(values);
        }

        public void SetSampleLabels(params PProfSampleLabel[] labels)
        {
            SetSampleLabels((IEnumerable<PProfSampleLabel>)labels);
        }

        public void SetSampleLabels(IEnumerable<PProfSampleLabel> labels)
        {
            if (SamplesCount == 0)
            {
                throw new InvalidOperationException($"Cannot invoke {nameof(SetSampleLabels)}(..) because"
                                                  + $" no samples were added to the current {nameof(PProfBuildSession)}.");
            }

            if (_lastSample.Label != null && _lastSample.Label.Count > 0)
            {
                throw new InvalidOperationException($"The labels for the {nameof(_lastSample)} of this {nameof(PProfBuildSession)} are already set."
                                                  + $" The labels for a particular sample cannot be modified once set.");
            }

            foreach (PProfSampleLabel label in labels)
            {
                PProfInfo.Label labelInfo = OwnerBuilder.CreateNewLabelInfo(label);

                AddToStringTable(labelInfo.KeyInfo);
                labelInfo.Item.Key = labelInfo.KeyInfo.OffsetInStringTable;

                switch (labelInfo.ValueKind)
                {
                    case PProfSampleLabel.Kind.Number:
                        AddToStringTable(labelInfo.NumberUnitInfo);
                        labelInfo.Item.NumUnit = labelInfo.NumberUnitInfo.OffsetInStringTable;
                        break;

                    case PProfSampleLabel.Kind.String:
                        AddToStringTable(labelInfo.StringValueInfo);
                        labelInfo.Item.Str = labelInfo.StringValueInfo.OffsetInStringTable;
                        break;

                    default:
                        throw new InvalidOperationException($"Invalid {nameof(labelInfo.ValueKind)}: '{labelInfo.ValueKind}'.");
                }

                _lastSample.Label.Add(labelInfo.Item);
            }
        }

        public bool TryAddLocationToLastSample(
                        LocationDescriptor locationDescriptor,
                        PProfBuilder.TryResolveLocationSymbolsDelegate tryResolveLocationSymbolsDelegate)
        {
            if (!OwnerBuilder.TryGetOrResolveCreatePProfLocationInfo(
                    locationDescriptor,
                    this,
                    tryResolveLocationSymbolsDelegate,
                    out PProfInfo.Location locationInfo))
            {
                return false;
            }

            // Functions:
            for (int i = 0; i < locationInfo.FunctionInfos.Count; i++)
            {
                PProfInfo.Function functionInfo = locationInfo.FunctionInfos[0];

                if (functionInfo.NameInfo != null)
                {
                    AddToStringTable(functionInfo.NameInfo);
                    functionInfo.Item.Name = functionInfo.NameInfo.OffsetInStringTable;
                }

                if (functionInfo.SystemNameInfo != null)
                {
                    AddToStringTable(functionInfo.SystemNameInfo);
                    functionInfo.Item.SystemName = functionInfo.SystemNameInfo.OffsetInStringTable;
                }

                if (functionInfo.FilenameInfo != null)
                {
                    AddToStringTable(functionInfo.FilenameInfo);
                    functionInfo.Item.Filename = functionInfo.FilenameInfo.OffsetInStringTable;
                }

                AddToFunctionsList(functionInfo);
            }

            // Mapping:
            PProfInfo.Mapping mappingInfo = locationInfo.MappingInfo;

            if (mappingInfo.FilenameInfo != null)
            {
                AddToStringTable(mappingInfo.FilenameInfo);
                mappingInfo.Item.Filename = mappingInfo.FilenameInfo.OffsetInStringTable;
            }

            if (mappingInfo.BuildIdInfo != null)
            {
                AddToStringTable(mappingInfo.BuildIdInfo);
                mappingInfo.Item.BuildId = mappingInfo.BuildIdInfo.OffsetInStringTable;
            }

            AddToMappingsList(mappingInfo);

            // Location:
            AddToLocationsList(locationInfo);

            // Sample:
            _lastSample.LocationId.Add(locationInfo.Item.Id);

            return true;
        }

        public void SetComment(string commentString)
        {
            if (_comment != null)
            {
                throw new InvalidOperationException($"The comment for this {nameof(PProfBuildSession)} was already set to {_comment}:"
                                                  + $" impossible to change to {commentString}.");
            }

            _comment = OwnerBuilder.GetOrCreateStringInfo(commentString);
            if (_comment != null)
            {
                AddToStringTable(_comment);
                _profile.Comment.Add(_comment.OffsetInStringTable);
            }
        }

        internal void InitProfile()
        {
            PProfInfo.String emptyStringInfo = OwnerBuilder.EmptyStringInfo;
            PProfInfo.Mapping entrypointMappingInfo = OwnerBuilder.GetMainEntrypointMappingInfo();

            // Empty string:
            AddToStringTable(emptyStringInfo);

            // Main entry-point mapping:
            if (entrypointMappingInfo != null)
            {
                AddToStringTable(entrypointMappingInfo.FilenameInfo);
                entrypointMappingInfo.Item.Filename = entrypointMappingInfo.FilenameInfo.OffsetInStringTable;

                if (entrypointMappingInfo.BuildIdInfo != null)
                {
                    AddToStringTable(entrypointMappingInfo.BuildIdInfo);
                    entrypointMappingInfo.Item.BuildId = entrypointMappingInfo.BuildIdInfo.OffsetInStringTable;
                }

                AddToMappingsList(entrypointMappingInfo);
            }

            // Sample measurement values types/units:
            for (int i = 0; i < OwnerBuilder.SampleValueTypes.Count; i++)
            {
                PProfInfo.ValueType sampleValueTypeInfo = OwnerBuilder.SampleValueTypeInfos[i];

                AddToStringTable(sampleValueTypeInfo.TypeInfo);
                AddToStringTable(sampleValueTypeInfo.UnitInfo);

                _profile.SampleType.Add(new PProfProto.ValueType()
                {
                    Type = sampleValueTypeInfo.TypeInfo.OffsetInStringTable,
                    Unit = sampleValueTypeInfo.UnitInfo.OffsetInStringTable
                });
            }

            // Drop frames RegEx:
            if (!string.IsNullOrWhiteSpace(OwnerBuilder.DropFramesRegExpInfo?.Item))
            {
                AddToStringTable(OwnerBuilder.DropFramesRegExpInfo);
                _profile.DropFrames = OwnerBuilder.DropFramesRegExpInfo.OffsetInStringTable;
            }

            // Keep frames RegEx:
            if (!string.IsNullOrWhiteSpace(OwnerBuilder.KeepFramesRegExpInfo?.Item))
            {
                AddToStringTable(OwnerBuilder.KeepFramesRegExpInfo);
                _profile.DropFrames = OwnerBuilder.KeepFramesRegExpInfo.OffsetInStringTable;
            }

            // Period measurement type/unit:
            if (OwnerBuilder.PeriodTypeInfo.HasNonDefaultValues)
            {
                AddToStringTable(OwnerBuilder.PeriodTypeInfo.TypeInfo);
                AddToStringTable(OwnerBuilder.PeriodTypeInfo.UnitInfo);

                _profile.PeriodType = new PProfProto.ValueType()
                {
                    Type = OwnerBuilder.PeriodTypeInfo.TypeInfo.OffsetInStringTable,
                    Unit = OwnerBuilder.PeriodTypeInfo.UnitInfo.OffsetInStringTable
                };

                _profile.Period = _period;
            }

            // Default sample type (not sure what DefaultSampleType is for; not supported for now):
            _profile.DefaultSampleType = ProtoConstants.NumericValue.UnsetInt64;
        }

        private void AddToStringTable(PProfInfo.String stringInfo)
        {
            if (stringInfo != null && !stringInfo.IsIncludedInSession)
            {
                PProfBuildSessionState buildState = ValidOwnerBuildState;
                stringInfo.OffsetInStringTable = buildState.StringTable.Count;
                buildState.StringTable.Add(stringInfo);

                _profile.StringTable.Add(stringInfo.Item);
            }
        }

        private void AddToLocationsList(PProfInfo.Location locationInfo)
        {
            if (locationInfo != null && !locationInfo.IsIncludedInSession)
            {
                PProfBuildSessionState buildState = ValidOwnerBuildState;
                locationInfo.IsIncludedInSession = true;
                buildState.Locations.Add(locationInfo);

                _profile.Location.Add(locationInfo.Item);
            }
        }

        private void AddToMappingsList(PProfInfo.Mapping mappingInfo)
        {
            if (mappingInfo != null && !mappingInfo.IsIncludedInSession)
            {
                PProfBuildSessionState buildState = ValidOwnerBuildState;
                mappingInfo.IsIncludedInSession = true;
                buildState.Mappings.Add(mappingInfo);

                _profile.Mapping.Add(mappingInfo.Item);
            }
        }

        private void AddToFunctionsList(PProfInfo.Function functionInfo)
        {
            if (functionInfo != null && !functionInfo.IsIncludedInSession)
            {
                PProfBuildSessionState buildState = ValidOwnerBuildState;
                functionInfo.IsIncludedInSession = true;
                buildState.Functions.Add(functionInfo);

                _profile.Function.Add(functionInfo.Item);
            }
        }
    }
}
