using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal class AdoNetClientInstrumentMethodAttribute : InstrumentMethodAttribute
    {
        public AdoNetClientInstrumentMethodAttribute(Type adoNetClientDataType)
        {
            AdoNetClientData = (IAdoNetClientData)Activator.CreateInstance(adoNetClientDataType);

            Assembly = AdoNetClientData.AssemblyName;
            Type = AdoNetClientData.SqlCommandType;
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
                Method = AdoNetConstants.MethodNames.ExecuteNonQueryAsync;
                ReturnTypeName = AdoNetConstants.TypeNames.IntTaskType;
                ParametersTypesNames = new[] { ClrNames.CancellationToken };
                CallTargetClass = typeof(CommandExecuteNonQueryAsyncIntegration);
            }
        }

        internal class CommandExecuteNonQueryAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteNonQueryAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                Method = AdoNetConstants.MethodNames.ExecuteNonQuery;
                ReturnTypeName = ClrNames.Int32;
                CallTargetClass = typeof(CommandExecuteNonQueryIntegration);
            }
        }

        internal class CommandExecuteReaderAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteReaderAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                Method = AdoNetConstants.MethodNames.ExecuteReaderAsync;
                ReturnTypeName = AdoNetClientData.DataReaderTaskType;
                ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken };
                CallTargetClass = typeof(CommandExecuteReaderAsyncIntegration);
            }
        }

        internal class CommandExecuteDbDataReaderAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteDbDataReaderAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                Method = AdoNetConstants.MethodNames.ExecuteDbDataReaderAsync;
                ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderTaskType;
                ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken };
                CallTargetClass = typeof(CommandExecuteReaderAsyncIntegration);
            }
        }

        internal class CommandExecuteReaderAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteReaderAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                Method = AdoNetConstants.MethodNames.ExecuteReader;
                ReturnTypeName = AdoNetClientData.DataReaderType;
                CallTargetClass = typeof(CommandExecuteReaderIntegration);
            }
        }

        internal class CommandExecuteReaderWithBehaviorAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteReaderWithBehaviorAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                Method = AdoNetConstants.MethodNames.ExecuteReader;
                ReturnTypeName = AdoNetClientData.DataReaderType;
                ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior };
                CallTargetClass = typeof(CommandExecuteReaderWithBehaviorIntegration);
            }
        }

        internal class CommandExecuteDbDataReaderWithBehaviorAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteDbDataReaderWithBehaviorAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                Method = AdoNetConstants.MethodNames.ExecuteDbDataReader;
                ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderType;
                ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior };
                CallTargetClass = typeof(CommandExecuteReaderWithBehaviorIntegration);
            }
        }

        internal class CommandExecuteScalarAsyncAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteScalarAsyncAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                Method = AdoNetConstants.MethodNames.ExecuteScalarAsync;
                ReturnTypeName = AdoNetConstants.TypeNames.ObjectTaskType;
                ParametersTypesNames = new[] { ClrNames.CancellationToken };
                CallTargetClass = typeof(CommandExecuteScalarAsyncIntegration);
            }
        }

        internal class CommandExecuteScalarAttribute : AdoNetClientInstrumentMethodAttribute
        {
            public CommandExecuteScalarAttribute(Type adoNetClientDataType)
                : base(adoNetClientDataType)
            {
                Method = AdoNetConstants.MethodNames.ExecuteScalar;
                ReturnTypeName = ClrNames.Object;
                CallTargetClass = typeof(CommandExecuteScalarIntegration);
            }
        }
    }
}
