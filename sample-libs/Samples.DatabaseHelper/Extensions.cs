using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Samples.DatabaseHelper
{
    public static class Extensions
    {
        public static IEnumerable<IDataRecord> AsDataRecords(this DbDataReader reader)
        {
            while (reader.Read())
            {
                yield return reader;
            }
        }

        public static DbParameter CreateParameterWithValue(this DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            return parameter;
        }

        public static DbParameter AddParameterWithValue(this DbCommand command, string name, object value)
        {
            DbParameter parameter = CreateParameterWithValue(command, name, value);
            command.Parameters.Add(parameter);
            return parameter;
        }
    }
}
