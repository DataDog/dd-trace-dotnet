// <copyright file="AdoNetClientInstrumentMethodAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal class AdoNetClientInstrumentMethodAttribute : InstrumentMethodAttribute
    {
        public AdoNetClientInstrumentMethodAttribute(Type adoNetClientDataType)
        {
            AdoNetClientData = (IAdoNetClientData)Activator.CreateInstance(adoNetClientDataType);

            AssemblyName = AdoNetClientData.AssemblyName;
            TypeName = AdoNetClientData.SqlCommandType;
            MinimumVersion = AdoNetClientData.MinimumVersion;
            MaximumVersion = AdoNetClientData.MaximumVersion;
            IntegrationName = AdoNetClientData.IntegrationName;
        }

        protected IAdoNetClientData AdoNetClientData { get; }

        internal class CommandExecuteNonQueryAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteNonQueryAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteNonQueryAsync;
                ReturnTypeName = AdoNetConstants.TypeNames.Int32TaskType;
                ParameterTypeNames = new[] { ClrNames.CancellationToken };
                CallTargetType = typeof(CommandExecuteNonQueryAsyncIntegration);
            }
        }

        internal class CommandExecuteNonQueryAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteNonQueryAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteNonQuery;
                ReturnTypeName = ClrNames.Int32;
                CallTargetType = typeof(CommandExecuteNonQueryIntegration);
            }
        }

        internal class CommandExecuteNonQueryWithBehaviorAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteNonQueryWithBehaviorAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteNonQuery;
                ReturnTypeName = ClrNames.Int32;
                ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior };
                CallTargetType = typeof(CommandExecuteNonQueryWithBehaviorIntegration);
            }
        }

        internal class CommandExecuteReaderAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteReaderAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync;
                ReturnTypeName = AdoNetClientData.DataReaderTaskType;
                CallTargetType = typeof(CommandExecuteReaderAsyncIntegration);
            }
        }

        internal class CommandExecuteReaderWithCancellationAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteReaderWithCancellationAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync;
                ReturnTypeName = AdoNetClientData.DataReaderTaskType;
                ParameterTypeNames = new[] { ClrNames.CancellationToken };
                CallTargetType = typeof(CommandExecuteReaderWithCancellationAsyncIntegration);
            }
        }

        internal class CommandExecuteReaderWithBehaviorAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteReaderWithBehaviorAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync;
                ReturnTypeName = AdoNetClientData.DataReaderTaskType;
                ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior };
                CallTargetType = typeof(CommandExecuteReaderWithBehaviorAsyncIntegration);
            }
        }

        internal class CommandExecuteReaderWithBehaviorAndCancellationAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteReaderWithBehaviorAndCancellationAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync;
                ReturnTypeName = AdoNetClientData.DataReaderTaskType;
                ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken };
                CallTargetType = typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncIntegration);
            }
        }

        internal class CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteDbDataReaderAsync;
                ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderTaskType;
                ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken };
                CallTargetType = typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncIntegration);
            }
        }

        internal class CommandExecuteReaderAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteReaderAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteReader;
                ReturnTypeName = AdoNetClientData.DataReaderType;
                CallTargetType = typeof(CommandExecuteReaderIntegration);
            }
        }

        internal class CommandExecuteReaderWithBehaviorAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteReaderWithBehaviorAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteReader;
                ReturnTypeName = AdoNetClientData.DataReaderType;
                ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior };
                CallTargetType = typeof(CommandExecuteReaderWithBehaviorIntegration);
            }
        }

        internal class CommandExecuteDbDataReaderWithBehaviorAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteDbDataReaderWithBehaviorAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteDbDataReader;
                ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderType;
                ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior };
                CallTargetType = typeof(CommandExecuteReaderWithBehaviorIntegration);
            }
        }

        internal class CommandExecuteScalarAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteScalarAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteScalarAsync;
                ReturnTypeName = AdoNetConstants.TypeNames.ObjectTaskType;
                ParameterTypeNames = new[] { ClrNames.CancellationToken };
                CallTargetType = typeof(CommandExecuteScalarAsyncIntegration);
            }
        }

        internal class CommandExecuteScalarAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteScalarAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteScalar;
                ReturnTypeName = ClrNames.Object;
                CallTargetType = typeof(CommandExecuteScalarIntegration);
            }
        }

        internal class CommandExecuteScalarWithBehaviorAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteScalarWithBehaviorAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteScalar;
                ReturnTypeName = ClrNames.Object;
                ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior };
                CallTargetType = typeof(CommandExecuteScalarWithBehaviorIntegration);
            }
        }
    }
}
