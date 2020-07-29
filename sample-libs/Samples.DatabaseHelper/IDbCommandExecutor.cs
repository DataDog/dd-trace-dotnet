using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.DatabaseHelper
{
    public interface IDbCommandExecutor
    {
        string CommandTypeName { get; }
        bool SupportsAsyncMethods { get; }

        void ExecuteNonQuery(IDbCommand command);
        Task ExecuteNonQueryAsync(IDbCommand command);
        Task ExecuteNonQueryAsync(IDbCommand command, CancellationToken cancellationToken);

        void ExecuteScalar(IDbCommand command);
        Task ExecuteScalarAsync(IDbCommand command);
        Task ExecuteScalarAsync(IDbCommand command, CancellationToken cancellationToken);

        void ExecuteReader(IDbCommand command);
        void ExecuteReader(IDbCommand command, CommandBehavior behavior);
        Task ExecuteReaderAsync(IDbCommand command);
        Task ExecuteReaderAsync(IDbCommand command, CommandBehavior behavior);
        Task ExecuteReaderAsync(IDbCommand command, CancellationToken cancellationToken);
        Task ExecuteReaderAsync(IDbCommand command, CommandBehavior behavior, CancellationToken cancellationToken);
    }
}
