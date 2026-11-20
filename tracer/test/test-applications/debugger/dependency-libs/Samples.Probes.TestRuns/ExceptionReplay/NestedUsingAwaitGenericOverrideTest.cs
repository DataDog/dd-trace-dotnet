using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#if NETCOREAPP3_0_OR_GREATER
namespace Samples.Probes.TestRuns.ExceptionReplay
{
    /// <summary>
    /// APMS-20228: customer-like async EH (nested <c>using</c> + <c>using (await ...)</c>,
    /// override <c>async Task&lt;T&gt;</c>, two result types). No probe or ExceptionReplay
    /// attributes — ProbesTests skips it, and it stays out of the skipped snapshot suite.
    /// </summary>
    public class NestedUsingAwaitGenericOverrideTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            var handler = new PaymentGetAllQueryHandler(new FakeConnFactory());
            await handler.Handle(new PaymentGetAllQuery { SelectCount = true }, CancellationToken.None);
        }

        public abstract class HandlerBase
        {
            public abstract Task<ListResponse<PaymentListModel>> Handle(PaymentGetAllQuery request, CancellationToken cancellationToken);
        }

        public class PaymentGetAllQueryHandler : HandlerBase
        {
            private readonly IConnFactory _connectionFactory;

            public PaymentGetAllQueryHandler(IConnFactory connectionFactory)
            {
                _connectionFactory = connectionFactory;
            }

            public override async Task<ListResponse<PaymentListModel>> Handle(PaymentGetAllQuery request, CancellationToken cancellationToken)
            {
                var filterParams = await request.GetFilterParamsAsync(cancellationToken);
                var sql = "select 1;" + (request.SelectCount ? "select 2" : null);
                var parameters = filterParams;

                IEnumerable<PaymentListModel> items;
                long totalCount = 0;
                using (var connection = _connectionFactory.CreateConnection())
                using (var queries = await connection.QueryMultipleAsync(sql, parameters, cancellationToken))
                {
                    items = await queries.ReadAsync();
                    if (request.SelectCount)
                    {
                        totalCount = await queries.ReadSingleAsyncLong();
                    }
                }

                var payments = items?.Select(x => new PaymentListModel()).ToList() ?? new List<PaymentListModel>();

                ListResponse<PaymentListModel> result = !request.SelectCount
                    ? new ListResponse<PaymentListModel>(payments)
                    : new SearchResponse<PaymentListModel>(payments, totalCount);

                throw new ExceptionReplayIntentionalException($"APMS-20228 nested using. Count={result.Items.Count}");
            }
        }

        public class ListResponse<T>
        {
            public ListResponse(List<T> items)
            {
                Items = items;
            }

            public List<T> Items { get; }
        }

        public class SearchResponse<T> : ListResponse<T>
        {
            public SearchResponse(List<T> items, long total)
                : base(items)
            {
                Total = total;
            }

            public long Total { get; }
        }

        public class PaymentListModel
        {
            public int Id { get; set; }
        }

        public class PaymentGetAllQuery
        {
            public bool SelectCount { get; set; }

            public Task<int> GetFilterParamsAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        }

        public interface IConnFactory
        {
            FakeConnection CreateConnection();
        }

        public sealed class FakeConnection : IDisposable
        {
            public Task<QueryMultiple> QueryMultipleAsync(string sql, object parameters, CancellationToken cancellationToken)
            {
                _ = sql;
                _ = parameters;
                _ = cancellationToken;
                return Task.FromResult(new QueryMultiple());
            }

            public void Dispose()
            {
            }
        }

        public sealed class QueryMultiple : IDisposable
        {
            public Task<IEnumerable<PaymentListModel>> ReadAsync() => Task.FromResult(Enumerable.Empty<PaymentListModel>());

            public Task<long> ReadSingleAsyncLong() => Task.FromResult(1L);

            public void Dispose()
            {
            }
        }

        private sealed class FakeConnFactory : IConnFactory
        {
            public FakeConnection CreateConnection() => new FakeConnection();
        }
    }
}
#endif
