using System;
using System.Data;

namespace Samples.DatabaseHelper
{
    /// <summary>
    /// Light wrapper around <see cref="IDbCommand"/> used to target calls in <c>netstandard.dll</c>.
    /// This project MUST target <c>netstandard2.0</c>.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class IDbCommandWrapper
    {
        private readonly IDbCommand _command;

        public IDbCommandWrapper(IDbCommand command)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
        }

        public int ExecuteNonQuery()
        {
            return _command.ExecuteNonQuery();
        }

        public IDataReader ExecuteReader()
        {
            return _command.ExecuteReader();
        }

        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            return _command.ExecuteReader(behavior);
        }

        public object ExecuteScalar()
        {
            return _command.ExecuteScalar();
        }
    }
}
