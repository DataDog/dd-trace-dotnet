// <copyright file="PProfInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Util;
using PProfProto = Perftools.Profiles;

namespace Datadog.PProf.Export
{
    internal static class PProfInfo
    {
        public struct ValueType
        {
            private readonly PProfInfo.String _typeInfo;
            private readonly PProfInfo.String _unitInfo;

            public ValueType(PProfSampleValueType valueTypeItems, PProfBuilder builder)
            {
                Validate.NotNull(builder, nameof(builder));

                _typeInfo = builder.GetOrCreateStringInfo(valueTypeItems.Type ?? string.Empty);
                _unitInfo = builder.GetOrCreateStringInfo(valueTypeItems.Unit ?? string.Empty);
            }

            public PProfInfo.String TypeInfo
            {
                get { return _typeInfo; }
            }

            public PProfInfo.String UnitInfo
            {
                get { return _unitInfo; }
            }

            public bool HasNonDefaultValues
            {
                get
                {
                    return string.IsNullOrEmpty(_typeInfo.Item) || string.IsNullOrEmpty(_unitInfo.Item);
                }
            }
        }

        public struct Label
        {
            private readonly PProfSampleLabel.Kind _valueKind;
            private readonly PProfInfo.String _keyInfo;
            private readonly PProfInfo.String _strValueOrNumUnitInfo;
            private readonly long _numValue;

            public Label(PProfSampleLabel labelItems, PProfItemCache builderCache, PProfProto.Label item)
            {
                Validate.NotNull(builderCache, nameof(builderCache));
                Validate.NotNull(item, nameof(item));

                this.Item = item;

                _valueKind = labelItems.ValueKind;
                _keyInfo = builderCache.GetOrCreateStringInfo(labelItems.Key ?? string.Empty);

                switch (labelItems.ValueKind)
                {
                    case PProfSampleLabel.Kind.Number:
                        _numValue = labelItems.NumberValue;
                        _strValueOrNumUnitInfo = builderCache.GetOrCreateStringInfo(labelItems.NumberUnit ?? string.Empty);
                        break;

                    case PProfSampleLabel.Kind.String:
                        _numValue = ProtoConstants.NumericValue.UnsetInt64;
                        _strValueOrNumUnitInfo = builderCache.GetOrCreateStringInfo(labelItems.StringValue ?? string.Empty);
                        break;

                    case PProfSampleLabel.Kind.Unknown:
                    default:
                        throw new InvalidOperationException($"Cannot create a {nameof(PProfInfo)}.{nameof(Label)},"
                                                          + $" because the {nameof(PProfSampleLabel.ValueKind)} of the"
                                                          + $" specified {nameof(PProfSampleLabel)} is {labelItems.ValueKind}."
                                                          + $" Either {PProfSampleLabel.Kind.Number} or {PProfSampleLabel.Kind.String} was expected."
                                                          + $" (Did you use the default ctor for {nameof(PProfSampleLabel)}?"
                                                          + " If so, use a different ctor overload.)");
                }
            }

            public PProfProto.Label Item { get; }

            public PProfSampleLabel.Kind ValueKind
            {
                get
                {
                    return _valueKind;
                }
            }

            public PProfInfo.String KeyInfo
            {
                get
                {
                    return _keyInfo;
                }
            }

            public long NumberValue
            {
                get
                {
                    PProfSampleLabel.ValidateValueKind(_valueKind, PProfSampleLabel.Kind.Number);
                    return _numValue;
                }
            }

            public PProfInfo.String StringValueInfo
            {
                get
                {
                    PProfSampleLabel.ValidateValueKind(_valueKind, PProfSampleLabel.Kind.String);
                    return _strValueOrNumUnitInfo;
                }
            }

            public PProfInfo.String NumberUnitInfo
            {
                get
                {
                    PProfSampleLabel.ValidateValueKind(_valueKind, PProfSampleLabel.Kind.Number);
                    return _strValueOrNumUnitInfo;
                }
            }
        }

        public class Location
        {
            public Location(PProfProto.Location item, PProfInfo.Mapping mappingInfo, PProfInfo.Function functionInfo)
                : this(item, mappingInfo, new PProfInfo.Function[1] { functionInfo })
            {
                Validate.NotNull(functionInfo, nameof(functionInfo));
            }

            public Location(PProfProto.Location item, PProfInfo.Mapping mappingInfo, IReadOnlyList<PProfInfo.Function> functionInfos)
            {
                Validate.NotNull(item, nameof(item));
                Validate.NotNull(mappingInfo, nameof(mappingInfo));
                Validate.NotNull(functionInfos, nameof(functionInfos));

                if (functionInfos.Count < 1)
                {
                    throw new ArgumentException($"{nameof(functionInfos)} must contain at least one element.");
                }

                if (item.Line.Count != functionInfos.Count)
                {
                    throw new ArgumentException($"{nameof(item)}.{nameof(item.Line)}.{nameof(item.Line.Count)} (={item.Line.Count})"
                                              + $" must have the same value as {nameof(functionInfos)}.{nameof(functionInfos.Count)} (={functionInfos.Count}).");
                }

                for (int i = 0; i < functionInfos.Count; i++)
                {
                    if (functionInfos[i] == null)
                    {
                        throw new ArgumentException($"{nameof(functionInfos)} must not contain null elements, but element at index {i} is null.");
                    }
                }

                this.Item = item;
                this.IsIncludedInSession = false;
                this.MappingInfo = mappingInfo;
                this.FunctionInfos = functionInfos;
            }

            public PProfProto.Location Item { get; }
            public bool IsIncludedInSession { get; set; }

            public PProfInfo.Mapping MappingInfo { get; }
            public IReadOnlyList<PProfInfo.Function> FunctionInfos { get; }
        }

        public class Mapping
        {
            public Mapping(PProfProto.Mapping item, PProfInfo.String filenameInfo)
                : this(item, filenameInfo, buildIdInfo: null)
            {
            }

            public Mapping(PProfProto.Mapping item, PProfInfo.String filenameInfo, PProfInfo.String buildIdInfo)
            {
                Validate.NotNull(item, nameof(item));
                Validate.NotNull(filenameInfo, nameof(filenameInfo));
                // buildIdInfo may be null

                this.Item = item;
                this.IsIncludedInSession = false;
                this.FilenameInfo = filenameInfo;
                this.BuildIdInfo = buildIdInfo;
            }

            public PProfProto.Mapping Item { get; }
            public bool IsIncludedInSession { get; set; }

            public PProfInfo.String FilenameInfo { get; }
            public PProfInfo.String BuildIdInfo { get; }
        }

        public class Function
        {
            public Function(PProfProto.Function item, PProfInfo.String nameInfo)
                : this(item, nameInfo, systemNameInfo: null, filenameInfo: null)
            {
            }

            public Function(PProfProto.Function item, PProfInfo.String nameInfo, PProfInfo.String systemNameInfo, PProfInfo.String filenameInfo)
            {
                Validate.NotNull(item, nameof(item));
                Validate.NotNull(nameInfo, nameof(nameInfo));
                // systemNameInfo may be null
                // filenameInfo may be null

                this.Item = item;
                this.IsIncludedInSession = false;
                this.NameInfo = nameInfo;
                this.SystemNameInfo = systemNameInfo;
                this.FilenameInfo = filenameInfo;
            }

            public PProfProto.Function Item { get; }
            public bool IsIncludedInSession { get; set; }

            public PProfInfo.String NameInfo { get; }
            public PProfInfo.String SystemNameInfo { get; }
            public PProfInfo.String FilenameInfo { get; }
        }

        public class String
        {
            public String(string item)
            {
                Validate.NotNull(item, nameof(item));
                this.Item = item;
                this.OffsetInStringTable = ProtoConstants.StringTableIndex.Unresolved;
            }

            public string Item { get; }
            public long OffsetInStringTable { get; set; }

            public bool IsIncludedInSession
            {
                get { return this.OffsetInStringTable != ProtoConstants.StringTableIndex.Unresolved; }
            }

            public void ResetOffsetInStringTable()
            {
                OffsetInStringTable = ProtoConstants.StringTableIndex.Unresolved;
            }

            public override string ToString()
            {
                return Item;
            }
        }
    }
}
