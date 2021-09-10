using System.Collections.Generic;
using System.Data;

namespace Samples.DatabaseHelper
{
    public static class Extensions
    {
        public static IEnumerable<IDataRecord> AsDataRecords(this IDataReader reader)
        {
            while (reader.Read())
            {
                yield return reader;
            }
        }

        public static IDbDataParameter CreateParameterWithValue(this IDbCommand command, string name, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            return parameter;
        }

        public static IDbDataParameter AddParameterWithValue(this IDbCommand command, string name, object value)
        {
            IDbDataParameter parameter = CreateParameterWithValue(command, name, value);
            command.Parameters.Add(parameter);
            return parameter;
        }
    }
}
