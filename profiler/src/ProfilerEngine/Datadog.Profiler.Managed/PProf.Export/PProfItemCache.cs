// <copyright file="PProfItemCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Util;

using PProfProto = Perftools.Profiles;

namespace Datadog.PProf.Export
{
    internal class PProfItemCache : IDisposable
    {
        private readonly PProfInfo.String _emptyStringInfo;

        private readonly Dictionary<LocationDescriptor, PProfInfo.Location> _locations = new Dictionary<LocationDescriptor, PProfInfo.Location>();
        private readonly Dictionary<string, PProfInfo.Mapping> _mappings = new Dictionary<string, PProfInfo.Mapping>();
        private readonly Dictionary<string, PProfInfo.Function> _functions = new Dictionary<string, PProfInfo.Function>();
        private readonly Dictionary<string, PProfInfo.String> _strings = new Dictionary<string, PProfInfo.String>();

        private long _lastUsedPProfLocationId = 0;
        private long _lastUsedPProfMappingId = 0;
        private long _lastUsedPProfFunctionId = 0;

        public PProfItemCache()
        {
            _emptyStringInfo = new PProfInfo.String(string.Empty);
            _strings.Add(string.Empty, _emptyStringInfo);
        }

        public PProfInfo.String EmptyStringInfo
        {
            get { return _emptyStringInfo; }
        }

        public void Dispose()
        {
            _locations.Clear();
            _mappings.Clear();
            _functions.Clear();
            _strings.Clear();
        }

        public bool TryGetLocationInfo(LocationDescriptor locationDescriptor, out PProfInfo.Location locationInfo)
        {
            return _locations.TryGetValue(locationDescriptor, out locationInfo);
        }

        public PProfInfo.Location GetOrCreateLocationInfo(
                                    LocationDescriptor locationDescriptor,
                                    PProfInfo.Mapping mappingInfo,
                                    PProfInfo.Function functionInfo)
        {
            return GetOrCreateLocationInfo(locationDescriptor, mappingInfo, new PProfInfo.Function[1] { functionInfo });
        }

        public PProfInfo.Location GetOrCreateLocationInfo(
                                    LocationDescriptor locationDescriptor,
                                    PProfInfo.Mapping mappingInfo,
                                    IReadOnlyList<PProfInfo.Function> functionInfos)
        {
            // locationDescriptor is value type so can never be null
            Validate.NotNull(mappingInfo, nameof(mappingInfo));
            Validate.NotNull(functionInfos, nameof(functionInfos));

            if (!_locations.TryGetValue(locationDescriptor, out PProfInfo.Location locationInfo))
            {
                var locationItem = new PProfProto.Location()
                {
                    Id = NextIdForLocation(),
                    MappingId = mappingInfo.Item.Id,
                    Address = ProtoConstants.NumericValue.UnsetUInt64,
                    IsFolded = false
                };

                for (int i = 0; i < functionInfos.Count; i++)
                {
                    if (functionInfos[i] != null)
                    {
                        locationItem.Line.Add(new PProfProto.Line()
                        {
                            FunctionId = functionInfos[i].Item.Id,
                            Line_ = ProtoConstants.NumericValue.UnsetInt64
                        });
                    }
                }

                locationInfo = new PProfInfo.Location(locationItem, mappingInfo, functionInfos);
                _locations.Add(locationDescriptor, locationInfo);
            }

            return locationInfo;
        }

        public bool TryGetMappingInfo(string moniker, out PProfInfo.Mapping mappingInfo)
        {
            mappingInfo = null;
            return (moniker != null) && _mappings.TryGetValue(moniker, out mappingInfo);
        }

        public PProfInfo.Mapping GetOrCreateMappingInfo(string moniker, string filename, string buildId)
        {
            Validate.NotNull(moniker, nameof(moniker));
            Validate.NotNull(filename, nameof(filename));
            // buildId may be null

            if (!_mappings.TryGetValue(moniker, out PProfInfo.Mapping mappingInfo))
            {
                mappingInfo = new PProfInfo.Mapping(
                        new PProfProto.Mapping()
                        {
                            Id = NextIdForMapping(),
                            MemoryStart = ProtoConstants.NumericValue.UnsetUInt64,
                            MemoryLimit = ProtoConstants.NumericValue.UnsetUInt64,
                            FileOffset = ProtoConstants.NumericValue.UnsetUInt64,
                            Filename = ProtoConstants.StringTableIndex.Unresolved,
                            BuildId = string.IsNullOrEmpty(buildId)
                                                    ? ProtoConstants.StringTableIndex.Unset
                                                    : ProtoConstants.StringTableIndex.Unresolved,
                            HasFunctions = true,
                            HasFilenames = true,
                            HasLineNumbers = false,
                            HasInlineFrames = false
                        },
                        GetOrCreateStringInfo(filename),
                        string.IsNullOrEmpty(buildId) ? null : GetOrCreateStringInfo(buildId));

                _mappings.Add(moniker, mappingInfo);
            }

            return mappingInfo;
        }

        public PProfInfo.Function GetOrCreateFunctionInfo(string moniker, string name, string systemName)
        {
            Validate.NotNull(moniker, nameof(moniker));
            Validate.NotNull(name, nameof(name));
            // systemName may be null

            if (!_functions.TryGetValue(moniker, out PProfInfo.Function functionInfo))
            {
                functionInfo = new PProfInfo.Function(
                        new PProfProto.Function()
                        {
                            Id = NextIdForFunction(),
                            Name = ProtoConstants.StringTableIndex.Unresolved,
                            SystemName = string.IsNullOrEmpty(systemName)
                                                    ? ProtoConstants.StringTableIndex.Unset
                                                    : ProtoConstants.StringTableIndex.Unresolved,
                            Filename = ProtoConstants.StringTableIndex.Unset,
                            StartLine = ProtoConstants.NumericValue.UnsetInt64,
                        },
                        GetOrCreateStringInfo(name),
                        string.IsNullOrEmpty(systemName) ? null : GetOrCreateStringInfo(systemName),
                        filenameInfo: null);

                _functions.Add(moniker, functionInfo);
            }

            return functionInfo;
        }

        public PProfInfo.String GetOrCreateStringInfo(string stringItem)
        {
            if (stringItem == null)
            {
                return null;
            }

            if (stringItem.Length == 0)
            {
                return _emptyStringInfo;
            }

            if (!_strings.TryGetValue(stringItem, out PProfInfo.String stringInfo))
            {
                stringInfo = new PProfInfo.String(stringItem);
                _strings.Add(stringItem, stringInfo);
            }

            return stringInfo;
        }

        public PProfInfo.Label CreateNewLabelInfo(PProfSampleLabel labelItems)
        {
            var labelItem = new PProfProto.Label();
            labelItem.Key = ProtoConstants.StringTableIndex.Unresolved;

            switch (labelItems.ValueKind)
            {
                case PProfSampleLabel.Kind.Number:
                    labelItem.Num = labelItems.NumberValue;
                    labelItem.NumUnit = ProtoConstants.StringTableIndex.Unresolved;
                    labelItem.Str = ProtoConstants.StringTableIndex.Unset;
                    break;

                case PProfSampleLabel.Kind.String:
                    labelItem.Num = ProtoConstants.NumericValue.UnsetInt64;
                    labelItem.NumUnit = ProtoConstants.StringTableIndex.Unset;
                    labelItem.Str = ProtoConstants.StringTableIndex.Unresolved;
                    break;

                case PProfSampleLabel.Kind.Unknown:
                default:
                    throw new InvalidOperationException($"Cannot create a {nameof(PProfProto)}.{nameof(PProfProto.Label)},"
                                                      + $" because the {nameof(PProfSampleLabel.ValueKind)} of the"
                                                      + $" specified {nameof(PProfSampleLabel)} is {labelItems.ValueKind}."
                                                      + $" Either {PProfSampleLabel.Kind.Number} or {PProfSampleLabel.Kind.String} was expected."
                                                      + $" (Did you use the default ctor for {nameof(PProfSampleLabel)}?"
                                                      + " If so, use a different ctor overload.)");
            }

            var labelInfo = new PProfInfo.Label(
                        labelItems,
                        this,
                        labelItem);

            return labelInfo;
        }

        private ulong NextIdForLocation()
        {
            return (ulong)++_lastUsedPProfLocationId;
        }

        private ulong NextIdForMapping()
        {
            return (ulong)++_lastUsedPProfMappingId;
        }

        private ulong NextIdForFunction()
        {
            return (ulong)++_lastUsedPProfFunctionId;
        }
    }
}
