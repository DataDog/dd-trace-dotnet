// <copyright file="PProfBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using PProfProto = Perftools.Profiles;

namespace Datadog.PProf.Export
{
    internal sealed class PProfBuilder
    {
        private readonly PProfItemCache _cache = new PProfItemCache();

        // The session lists logically belong to a session instance.
        // However, we only have one concurrent session at a time and those lists can be long, so that the underlying objects can get into the LOH.
        // So we keep them here and clear them after each session.
        private readonly PProfBuildSessionState _buildState = new PProfBuildSessionState();

        private string _mainEntrypointMappingMoniker = null;
        private PProfInfo.String _dropFramesRegExpInfo = null;
        private PProfInfo.String _keepFramesRegExpInfo = null;
        private PProfInfo.ValueType _periodTypeInfo;

        public PProfBuilder()
        {
            _periodTypeInfo = new PProfInfo.ValueType(new PProfSampleValueType(type: null, unit: null), this);

            this.SymbolMonikersConfig = new SymbolMonikersConfigSettings(this);
            this.SampleValueTypes = new PProfSampleValueType[0];
            this.SampleValueTypeInfos = new PProfInfo.ValueType[0];
            this.SampleValuesUnset = new long[0];
        }

        public delegate bool TryResolveLocationSymbolsDelegate(
            PProfBuildSession pprofBuildSession,
            LocationDescriptor locationDescriptor,
            out string functionName,
            out string classTypeName,
            out string binaryContainerName,
            out string binaryContainerVersion);

        public IReadOnlyList<PProfSampleValueType> SampleValueTypes { get; private set; }

        public IReadOnlyList<PProfInfo.ValueType> SampleValueTypeInfos { get; private set; }

        public SymbolMonikersConfigSettings SymbolMonikersConfig { get; }

        public string DropFramesRegExp
        {
            get
            {
                return _dropFramesRegExpInfo?.Item;
            }

            set
            {
                EnsureNoSessionState(nameof(DropFramesRegExp), "set_");
                _dropFramesRegExpInfo = GetOrCreateStringInfo(value);
            }
        }

        public string KeepFramesRegExp
        {
            get
            {
                return _keepFramesRegExpInfo?.Item;
            }

            set
            {
                EnsureNoSessionState(nameof(KeepFramesRegExp), "set_");
                _keepFramesRegExpInfo = GetOrCreateStringInfo(value);
            }
        }

        public PProfSampleValueType PeriodType
        {
            get
            {
                return new PProfSampleValueType(_periodTypeInfo.TypeInfo.Item, _periodTypeInfo.TypeInfo.Item);
            }

            set
            {
                PeriodTypeInfo = new PProfInfo.ValueType(value, this);
            }
        }

        internal PProfInfo.String KeepFramesRegExpInfo
        {
            get { return _keepFramesRegExpInfo; }
        }

        internal PProfInfo.String DropFramesRegExpInfo
        {
            get { return _dropFramesRegExpInfo; }
        }

        internal PProfInfo.ValueType PeriodTypeInfo
        {
            get
            {
                return _periodTypeInfo;
            }

            private set
            {
                EnsureNoSessionState(nameof(PeriodType), "set_");
                _periodTypeInfo = value;
            }
        }

        internal long[] SampleValuesUnset { get; private set; }

        internal PProfInfo.String EmptyStringInfo
        {
            get { return _cache.EmptyStringInfo; }
        }

        public void Dispose()
        {
            using (StartNewPProfBuildSession())
            {
                _cache.Dispose();
            }
        }

        public PProfBuildSession StartNewPProfBuildSession()
        {
            bool lockTaken = false;
            Monitor.Enter(_buildState.SessionLock, ref lockTaken);

            if (!lockTaken)
            {
                throw new InvalidOperationException($"Cannot enter a monitor on {nameof(_buildState)}.{nameof(_buildState.SessionLock)}.");
            }

            SymbolMonikersConfig.SetReadOnly();
            _buildState.Session = new PProfBuildSession(this, _buildState);
            _buildState.Session.InitProfile();
            return _buildState.Session;
        }

        public void SetSampleValueTypes(params PProfSampleValueType[] valueTypes)
        {
            SetSampleValueTypes((IEnumerable<PProfSampleValueType>)valueTypes);
        }

        public void SetSampleValueTypes(IEnumerable<PProfSampleValueType> valueTypes)
        {
            if (_buildState.Session != null && _buildState.Session.SamplesCount > 0)
            {
                throw new InvalidOperationException($"Cannot invoke {nameof(SetSampleValueTypes)}(..) when there is an active "
                                                  + $" {nameof(PProfBuildSession)} and {_buildState.Session.SamplesCount} samples have been added to that session.");
            }

            if (valueTypes == null)
            {
                SampleValueTypes = new PProfSampleValueType[0];
                SampleValueTypeInfos = new PProfInfo.ValueType[0];
                SampleValuesUnset = new long[0];
                return;
            }

            int valueTypesCount;
            if (valueTypes is ICollection<PProfSampleValueType> valueTypesCollection)
            {
                valueTypesCount = valueTypesCollection.Count;
            }
            else if (valueTypes is IReadOnlyCollection<PProfSampleValueType> valueTypesROCollection)
            {
                valueTypesCount = valueTypesROCollection.Count;
            }
            else
            {
                valueTypesCount = valueTypes.Count();
            }

            var newSampleValueTypes = new PProfSampleValueType[valueTypesCount];
            var newSampleValueTypeInfos = new PProfInfo.ValueType[valueTypesCount];
            var newSampleValuesUnset = new long[valueTypesCount];

            int i = 0;
            foreach (PProfSampleValueType vt in valueTypes)
            {
                newSampleValueTypes[i] = vt;
                newSampleValueTypeInfos[i] = new PProfInfo.ValueType(vt, this);
                newSampleValuesUnset[i] = ProtoConstants.NumericValue.UnsetInt64;
                i++;
            }

            SampleValueTypes = newSampleValueTypes;
            SampleValueTypeInfos = newSampleValueTypeInfos;
            SampleValuesUnset = newSampleValuesUnset;
        }

        public PProfInfo.Mapping GetMainEntrypointMappingInfo()
        {
            if (_cache.TryGetMappingInfo(_mainEntrypointMappingMoniker, out PProfInfo.Mapping mainEntrypointMappingInfo))
            {
                return mainEntrypointMappingInfo;
            }

            return null;
        }

        public bool TrySetMainEntrypointMappingInfo(string entrypointBinaryContainerName, string entrypointBinaryContainerVersion)
        {
            return TrySetMainEntrypointMappingInfo(entrypointBinaryContainerName, entrypointBinaryContainerVersion, out PProfInfo.Mapping _);
        }

        public bool TrySetMainEntrypointMappingInfo(
                        string entrypointBinaryContainerName,
                        string entrypointBinaryContainerVersion,
                        out PProfInfo.Mapping mainEntrypointMappingInfo)
        {
            if (_buildState.Session != null)
            {
                mainEntrypointMappingInfo = null;
                return false;
            }

            mainEntrypointMappingInfo = SetMainEntrypointMappingInfo(entrypointBinaryContainerName, entrypointBinaryContainerVersion);
            return true;
        }

        internal bool TryFinishPProfBuildSessionInternal(PProfBuildSession session)
        {
            // This method should only be invoked from PProfBuildSession.Dispose().
            if (session == null || _buildState.Session != session)
            {
                return false;
            }

            _buildState.ResetSession();
            Monitor.Exit(_buildState.SessionLock);
            return true;
        }

        internal PProfInfo.Mapping GetOrCreateMainEntrypointMappingInfo()
        {
            // This method must only be called by PProfBuildSession when it's safe!
            // (I.e. at the very begining of the session, before anything has been added to the session lists.)
            PProfInfo.Mapping mainEntrypointMappingInfo = GetMainEntrypointMappingInfo();
            if (mainEntrypointMappingInfo == null)
            {
                mainEntrypointMappingInfo = SetMainEntrypointMappingInfo(entrypointBinaryContainerName: null, entrypointBinaryContainerVersion: null);
            }

            return mainEntrypointMappingInfo;
        }

        internal bool TryGetOrResolveCreatePProfLocationInfo(
                LocationDescriptor locationDescriptor,
                PProfBuildSession pprofBuildSession,
                TryResolveLocationSymbolsDelegate tryResolveLocationSymbolsDelegate,
                out PProfInfo.Location locationInfo)
        {
            if (_cache.TryGetLocationInfo(locationDescriptor, out locationInfo))
            {
                return true;
            }

            if (
                tryResolveLocationSymbolsDelegate == null ||
                !tryResolveLocationSymbolsDelegate(
                    pprofBuildSession,
                    locationDescriptor,
                    out string functionName,
                    out string classTypeName,
                    out string binaryContainerName,
                    out string binaryContainerVersion))
            {
                locationInfo = null;
                return false;
            }

            BuildMonikers(
                ref functionName,
                ref classTypeName,
                ref binaryContainerName,
                ref binaryContainerVersion,
                out string functionMoniker,
                out string binaryContainerMoniker);

            // system name is not used so pass null (i.e. empty string)
            PProfInfo.Function functionInfo = _cache.GetOrCreateFunctionInfo(functionMoniker, functionMoniker, null);
            PProfInfo.Mapping mappingInfo = _cache.GetOrCreateMappingInfo(binaryContainerMoniker, binaryContainerName, binaryContainerVersion);
            locationInfo = _cache.GetOrCreateLocationInfo(locationDescriptor, mappingInfo, functionInfo);

            return true;
        }

        internal PProfInfo.String GetOrCreateStringInfo(string stringItem)
        {
            return _cache.GetOrCreateStringInfo(stringItem);
        }

        internal PProfInfo.Label CreateNewLabelInfo(PProfSampleLabel labelItems)
        {
            return _cache.CreateNewLabelInfo(labelItems);
        }

        private PProfInfo.Mapping SetMainEntrypointMappingInfo(string entrypointBinaryContainerName, string entrypointBinaryContainerVersion)
        {
            EnsureNoSessionState(nameof(SetMainEntrypointMappingInfo));

            string newMainEntrypointMappingMoniker =
                BuildBinaryContainerMoniker(ref entrypointBinaryContainerName, ref entrypointBinaryContainerVersion);

            PProfInfo.Mapping newMainEntrypointMappingInfo = _cache
                .GetOrCreateMappingInfo(newMainEntrypointMappingMoniker, entrypointBinaryContainerName, entrypointBinaryContainerVersion);

            // It is tempting to remove the mapping keyed by _mainEntrypointMappingMoniker from _cache.Mappings,
            // but we cannot because that mapping may be used elsewhere.
            _mainEntrypointMappingMoniker = newMainEntrypointMappingMoniker;

            return newMainEntrypointMappingInfo;
        }

        private void EnsureNoSessionState(string apiNameBeingCalled, string apiNamePrefix = null)
        {
            if (_buildState.HasSesionListItems)
            {
                if (apiNamePrefix != null)
                {
                    apiNameBeingCalled = apiNamePrefix + apiNameBeingCalled;
                }

                throw new InvalidOperationException($"{apiNameBeingCalled} may only be called"
                                                  + $" between active {nameof(PProfBuildSession)}s"
                                                  + $" (or, if a session is active, when the corresponding"
                                                  + $" {nameof(_buildState)}'s session lists are empty).");
            }
        }

        private string BuildBinaryContainerMoniker(ref string binaryContainerName, ref string binaryContainerVersion)
        {
            binaryContainerName = string.IsNullOrEmpty(binaryContainerName)
                                            ? SymbolMonikersConfig.UnknownBinaryContainerName
                                            : binaryContainerName;

            binaryContainerVersion = (string.IsNullOrEmpty(binaryContainerVersion) && SymbolMonikersConfig.DisplayVersionIfUnknown)
                                            ? SymbolMonikersConfig.UnknownBinaryContainerVersion
                                            : binaryContainerVersion;

            if (string.IsNullOrEmpty(binaryContainerVersion))
            {
                return binaryContainerName;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(binaryContainerVersion))
                {
                    binaryContainerVersion = '"' + binaryContainerVersion + '"';
                }

                string template = SymbolMonikersConfig.BinaryContainerWithVersionTemplate;
                if (string.IsNullOrWhiteSpace(template))
                {
                    template = "{0}(var: {1})";
                }

                return string.Format(template, binaryContainerName, binaryContainerVersion);
            }
        }

        private void BuildMonikers(
                        ref string functionName,
                        ref string classTypeName,
                        ref string binaryContainerName,
                        ref string binaryContainerVersion,
                        out string functionMoniker,
                        out string binaryContainerMoniker)
        {
            functionName = functionName ?? SymbolMonikersConfig.UnknownFunctionName;
            classTypeName = classTypeName ?? SymbolMonikersConfig.UnknownClassTypeName;
            binaryContainerMoniker = BuildBinaryContainerMoniker(ref binaryContainerName, ref binaryContainerVersion);

            var name = new StringBuilder();

            if (SymbolMonikersConfig.IncludeBinaryContainerNameIntoApiName)
            {
                name.Append(SymbolMonikersConfig.PrefixBinaryContainerName ?? string.Empty);
                name.Append(binaryContainerMoniker);
            }

            string prefixNamespaceName = SymbolMonikersConfig.PrefixNamespaceName ?? string.Empty;
            string prefixClassTypeName = SymbolMonikersConfig.PrefixClassTypeName ?? string.Empty;

            // Need to refactor the prefixing to be all in one place and remove this condition.
            string classTypeNameTrimmed = classTypeName.Trim();
            if (!classTypeNameTrimmed.StartsWith(prefixNamespaceName) && !classTypeNameTrimmed.StartsWith(prefixClassTypeName))
            {
                name.Append(prefixClassTypeName);
            }

            name.Append(classTypeName);

            name.Append(SymbolMonikersConfig.PrefixFunctionName ?? string.Empty);
            name.Append(functionName);

            functionMoniker = name.ToString();
        }
    }
}
