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
                CallTargetClassType = typeof(CommandExecuteNonQueryAsyncIntegration);
            }
        }

        internal class CommandExecuteNonQueryAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteNonQueryAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteNonQuery;
                ReturnTypeName = ClrNames.Int32;
                CallTargetClassType = typeof(CommandExecuteNonQueryIntegration);
            }
        }

        internal class CommandExecuteReaderAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteReaderAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteReaderAsync;
                ReturnTypeName = AdoNetClientData.DataReaderTaskType;
                ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken };
                CallTargetClassType = typeof(CommandExecuteReaderAsyncIntegration);
            }
        }

        internal class CommandExecuteDbDataReaderAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteDbDataReaderAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteDbDataReaderAsync;
                ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderTaskType;
                ParameterTypeNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken };
                CallTargetClassType = typeof(CommandExecuteReaderAsyncIntegration);
            }
        }

        internal class CommandExecuteReaderAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteReaderAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteReader;
                ReturnTypeName = AdoNetClientData.DataReaderType;
                CallTargetClassType = typeof(CommandExecuteReaderIntegration);
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
                CallTargetClassType = typeof(CommandExecuteReaderWithBehaviorIntegration);
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
                CallTargetClassType = typeof(CommandExecuteReaderWithBehaviorIntegration);
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
                CallTargetClassType = typeof(CommandExecuteScalarAsyncIntegration);
            }
        }

        internal class CommandExecuteScalarAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteScalarAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                MethodName = AdoNetConstants.MethodNames.ExecuteScalar;
                ReturnTypeName = ClrNames.Object;
                CallTargetClassType = typeof(CommandExecuteScalarIntegration);
            }
        }
    }
}
