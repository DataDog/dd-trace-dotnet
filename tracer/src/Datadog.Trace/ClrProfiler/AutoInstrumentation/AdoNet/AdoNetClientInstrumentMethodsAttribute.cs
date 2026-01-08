// <copyright file="AdoNetClientInstrumentMethodsAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    /// <summary>
    /// Attribute that indicates that the decorated class is meant to intercept a method
    /// by modifying the method body with callbacks. Used to generate the integration definitions file.
    /// </summary>
    /// <remarks>
    /// Beware that the fullname of this class is being used for App Trimming support in the _build/Build.Steps.cs file
    /// as string. Avoid changing the name and/or namespace of this class.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class AdoNetClientInstrumentMethodsAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the assembly that contains the target method to be intercepted.
        /// Required.
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Gets or sets the name of the type that contain the target method to be intercepted.
        /// Required.
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Gets or sets the target minimum version.
        /// </summary>
        public string MinimumVersion { get; set; }

        /// <summary>
        /// Gets or sets the target maximum version.
        /// </summary>
        public string MaximumVersion { get; set; }

        /// <summary>
        /// Gets or sets the integration name. Allows to group several integration with a single integration name.
        /// </summary>
        public string IntegrationName { get; set; }

        /// <summary>
        /// Gets or sets the DataReader type to use with target signatures that require it
        /// Required.
        /// </summary>
        public string DataReaderType { get; set; }

        /// <summary>
        /// Gets or sets the DataReader type to use with target signatures that require it
        /// Required.
        /// </summary>
        public string DataReaderTaskType { get; set; }

        /// <summary>
        /// Gets or sets the names of attributes decorated with <see cref="AdoNetTargetSignatureAttribute"/>.
        /// Describes all the signatures to instrument
        /// Required.
        /// </summary>
        public Type[] TargetMethodAttributes { get; set; }

        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class AdoNetTargetSignatureAttribute : Attribute
        {
            internal enum AdoNetTargetSignatureReturnType
            {
                /// <summary>
                ///  Uses the fixed return type specified in <see cref="AdoNetTargetSignatureAttribute.ReturnTypeName" />
                /// </summary>
                Default,

                /// <summary>
                ///  Uses the return type specified in <see cref="AdoNetClientInstrumentMethodsAttribute.DataReaderType" />
                /// </summary>
                DataReaderType,

                /// <summary>
                ///  Uses the return type specified in <see cref="AdoNetClientInstrumentMethodsAttribute.DataReaderTaskType" />
                /// </summary>
                DataReaderTaskType,
            }

            /// <summary>
            /// Gets or sets the name of the target method to be intercepted.
            /// If null, default to the name of the decorated method.
            /// </summary>
            public string MethodName { get; set; }

            /// <summary>
            /// Gets or sets the return type name
            /// </summary>
            public string ReturnTypeName { get; set; }

            /// <summary>
            /// Gets or sets the parameters type array for the target method to be intercepted.
            /// </summary>
            public string[] ParameterTypeNames { get; set; }

            /// <summary>
            /// Gets or sets the return type to use with this signature
            /// </summary>
            public AdoNetTargetSignatureReturnType ReturnType { get; set; } = AdoNetTargetSignatureReturnType.Default;

            /// <summary>
            /// Gets or sets the CallTarget Class used to instrument the method
            /// </summary>
            public Type CallTargetType { get; set; }

            /// <summary>
            /// Gets or sets the CallTarget integration type
            /// </summary>
            public CallTargetKind CallTargetIntegrationKind { get; set; } = CallTargetKind.Default;

            /// <summary>
            /// Gets or sets the call target instrumentation category
            /// </summary>
            public InstrumentationCategory InstrumentationCategory { get; set; } = InstrumentationCategory.Tracing;
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteNonQueryAsync,
            ReturnTypeName = AdoNetConstants.TypeNames.Int32TaskType,
            ParameterTypeNames = new[] { ClrNames.CancellationToken },
            CallTargetType = typeof(CommandExecuteNonQueryAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteNonQueryAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteNonQuery,
            ReturnTypeName = ClrNames.Int32,
            CallTargetType = typeof(CommandExecuteNonQueryIntegration),
            CallTargetIntegrationKind = CallTargetKind.Default)]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteNonQueryAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteNonQuery,
            ReturnTypeName = ClrNames.Int32,
            CallTargetType = typeof(CommandExecuteNonQueryIntegration),
            CallTargetIntegrationKind = CallTargetKind.Derived)]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteNonQueryDerivedAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteNonQuery,
            ReturnTypeName = ClrNames.Int32,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetType = typeof(CommandExecuteNonQueryWithBehaviorIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteNonQueryWithBehaviorAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderTaskType,
            CallTargetType = typeof(CommandExecuteReaderAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteReaderAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderTaskType,
            ParameterTypeNames = new[] { ClrNames.CancellationToken },
            CallTargetType = typeof(CommandExecuteReaderWithCancellationAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteReaderWithCancellationAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderTaskType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteReaderWithBehaviorAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderTaskType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteReaderWithBehaviorAndCancellationAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteDbDataReaderAsync,
            ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderTaskType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReader,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderType,
            CallTargetType = typeof(CommandExecuteReaderIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteReaderAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteReader,
            ReturnType = AdoNetTargetSignatureAttribute.AdoNetTargetSignatureReturnType.DataReaderType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteReaderWithBehaviorAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteDbDataReader,
            ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteDbDataReaderWithBehaviorAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteDbDataReader,
            ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderType,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetIntegrationKind = CallTargetKind.Derived,
            CallTargetType = typeof(CommandExecuteReaderWithBehaviorIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteDbDataReaderWithBehaviorDerivedAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteScalarAsync,
            ReturnTypeName = AdoNetConstants.TypeNames.ObjectTaskType,
            ParameterTypeNames = new[] { ClrNames.CancellationToken },
            CallTargetType = typeof(CommandExecuteScalarAsyncIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteScalarAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteScalar,
            ReturnTypeName = ClrNames.Object,
            CallTargetType = typeof(CommandExecuteScalarIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteScalarAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteScalar,
            ReturnTypeName = ClrNames.Object,
            CallTargetIntegrationKind = CallTargetKind.Derived,
            CallTargetType = typeof(CommandExecuteScalarIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteScalarDerivedAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ExecuteScalar,
            ReturnTypeName = ClrNames.Object,
            ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior },
            CallTargetType = typeof(CommandExecuteScalarWithBehaviorIntegration))]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class CommandExecuteScalarWithBehaviorAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.Read,
            ReturnTypeName = ClrNames.Bool,
            CallTargetType = typeof(ReaderReadIntegration),
            InstrumentationCategory = InstrumentationCategory.Iast)]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class IastReaderReadAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.ReadAsync,
            ReturnTypeName = AdoNetConstants.TypeNames.ObjectTaskType,
            ParameterTypeNames = new[] { ClrNames.CancellationToken },
            CallTargetType = typeof(ReaderReadAsyncIntegration),
            InstrumentationCategory = InstrumentationCategory.Iast)]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class IastReaderReadAsyncAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.Close,
            ReturnTypeName = ClrNames.Void,
            CallTargetType = typeof(ReaderCloseIntegration),
            InstrumentationCategory = InstrumentationCategory.Iast)]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class IastReaderCloseAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.GetString,
            ReturnTypeName = ClrNames.String,
            ParameterTypeNames = new[] { ClrNames.Int32 },
            CallTargetType = typeof(ReaderGetStringIntegration),
            InstrumentationCategory = InstrumentationCategory.Iast)]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class IastReaderGetStringAttribute : Attribute
        {
        }

        [AdoNetTargetSignature(
            MethodName = AdoNetConstants.MethodNames.GetValue,
            ReturnTypeName = ClrNames.Object,
            ParameterTypeNames = new[] { ClrNames.Int32 },
            CallTargetType = typeof(ReaderGetStringIntegration),
            InstrumentationCategory = InstrumentationCategory.Iast)]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class IastReaderGetValueAttribute : Attribute
        {
        }
    }
}
