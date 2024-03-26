// <copyright file="Constants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.SourceGenerators.InstrumentationDefinitions;

internal static class Constants
{
    public const string InstrumentAttribute = "Datadog.Trace.ClrProfiler.InstrumentMethodAttribute";
    public const string AdoNetInstrumentAttribute = "Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute";
    public const string AdoNetTargetSignatureAttribute = AdoNetInstrumentAttribute + ".AdoNetTargetSignatureAttribute";
    public const string AdoNetTargetSignatureSymbolName = AdoNetInstrumentAttribute + "+AdoNetTargetSignatureAttribute";

    public static class InstrumentAttributeProperties
    {
        public const string AssemblyName = nameof(AssemblyName);
        public const string AssemblyNames = nameof(AssemblyNames);
        public const string TypeName = nameof(TypeName);
        public const string TypeNames = nameof(TypeNames);
        public const string MethodName = nameof(MethodName);
        public const string ReturnTypeName = nameof(ReturnTypeName);
        public const string ParameterTypeNames = nameof(ParameterTypeNames);
        public const string MinimumVersion = nameof(MinimumVersion);
        public const string MaximumVersion = nameof(MaximumVersion);
        public const string IntegrationName = nameof(IntegrationName);
        public const string CallTargetType = nameof(CallTargetType);
        public const string CallTargetIntegrationKind = nameof(CallTargetIntegrationKind);
        public const string InstrumentationCategory = nameof(InstrumentationCategory);
    }

    public static class AdoNetSignatureAttributeProperties
    {
        public const string MethodName = nameof(MethodName);
        public const string ReturnTypeName = nameof(ReturnTypeName);
        public const string ParameterTypeNames = nameof(ParameterTypeNames);
        public const string CallTargetType = nameof(CallTargetType);
        public const string CallTargetIntegrationKind = nameof(CallTargetIntegrationKind);
        public const string ReturnType = nameof(ReturnType);
    }

    public static class AdoNetInstrumentAttributeProperties
    {
        public const string AssemblyName = nameof(AssemblyName);
        public const string TypeName = nameof(TypeName);
        public const string MinimumVersion = nameof(MinimumVersion);
        public const string MaximumVersion = nameof(MaximumVersion);
        public const string IntegrationName = nameof(IntegrationName);
        public const string DataReaderType = nameof(DataReaderType);
        public const string DataReaderTaskType = nameof(DataReaderTaskType);
        public const string TargetMethodAttributes = nameof(TargetMethodAttributes);
    }
}
