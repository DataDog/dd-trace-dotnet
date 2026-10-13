using System;
using System.Threading.Tasks;

#if NETCOREAPP3_0_OR_GREATER
namespace Samples.Probes.TestRuns.ExceptionReplay
{
    /// <summary>
    /// APMS-20228: adversarial async EH (<c>await using</c>, <c>catch when</c>, await in <c>finally</c>).
    /// No probe or ExceptionReplay attributes — ProbesTests skips it, and it stays out of the skipped snapshot suite.
    /// </summary>
    public class AwaitUsingCatchFilterFinallyTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            await Task.Yield();

            try
            {
                await using var reader = await CreateReaderAsync().ConfigureAwait(false);
                try
                {
                    await Task.Yield();
                    throw new InvalidOperationException("APMS-20228 inner");
                }
                finally
                {
                    await Task.Yield();
                    _ = reader;
                }
            }
            catch (Exception ex) when (ex is not ExceptionReplayIntentionalException)
            {
                throw new ExceptionReplayIntentionalException("APMS-20228 await using catch filter", ex);
            }
        }

        private static async Task<RelationalDataReader> CreateReaderAsync()
        {
            await Task.Yield();
            return new RelationalDataReader();
        }

        public sealed class RelationalDataReader : IAsyncDisposable, IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                GC.SuppressFinalize(this);
            }

            public async ValueTask DisposeAsync()
            {
                if (_disposed)
                {
                    return;
                }

                await Task.Yield();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
#endif
