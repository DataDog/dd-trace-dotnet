using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

#if !NET461_OR_GREATER && !NETCOREAPP2_1

namespace Samples.Probes.TestRuns.ExceptionReplay
{
    [ExceptionReplayTestData(expectedNumberOfSnapshotsDefault: 4, expectedNumberOfSnaphotsFull: 4)]
    internal class ThrowOnlyOnceTest : IAsyncRun
    {
        private static bool _hasThrown;

        public async Task RunAsync()
        {
            if (_hasThrown)
            {
                return;
            }

            await Task.Yield();

            try
            {
                try
                {
                    await Task.Yield();
                    await RunAsync2();
                }
                catch (Exception e)
                {
                    await Task.Yield();
                    await Task.Yield();
                    await Task.Yield();
                    throw;
                }
            }
            finally
            {
                _hasThrown = true;
                await Task.Yield();
            }

        }

        public async Task RunAsync2()
        {
            await Task.Yield();

            try
            {
                try
                {
                    await Task.Yield();
                    await InTheMiddle();
                }
                catch (Exception e)
                {
                    //await Task.Yield();
                    //await Task.Yield();
                    //await Task.Yield();
                    throw;
                    await Task.Yield();
                }
            }
            catch (Exception ex)
            {
                throw;
                await Task.Yield();
            }

        }

        public async Task InTheMiddle()
        {
            int num = 3;
            try
            {
                await Task.Yield();
                RelationalDataReader _ = await AwaitUsingFunc().ConfigureAwait(false);
                Exception obj = null;
                try
                {
                    await InTheMiddle2();
                }
                catch (Exception obj2)
                {
                    obj = obj2;
                }

                if (_ != null)
                {
                    await ((IAsyncDisposable)_).DisposeAsync();
                }
                Exception obj3 = obj;
                if (obj3 != null)
                {
                    Exception obj4 = obj3 as Exception;
                    if (obj4 == null)
                    {
                        throw obj3;
                    }

                    ExceptionDispatchInfo.Capture(obj4).Throw();
                }
            }
            catch (Exception ex) when (!(ex is NotImplementedException))
            {
                throw new NotImplementedException("Outer", ex);
            }
        }

        public async Task InTheMiddle2()
        {
            try
            {
                await Task.Yield();
                await using var _ = await AwaitUsingFunc().ConfigureAwait(false);
                await Foo();
            }
            catch (Exception e) when (e is not NotImplementedException)
            {
                throw new NotImplementedException("Outer", inner: e);
            }
        }

        public class RelationalDataReader : IAsyncDisposable, IDisposable
        {
            private bool _disposed = false;
            private bool _disposedAsync = false;

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        // Dispose managed resources
                    }

                    // Dispose unmanaged resources
                    _disposed = true;
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (!_disposed)
                {
                    // Perform async cleanup here
                    await DisposeAsyncCore().ConfigureAwait(false);

                    _disposed = true;
                    GC.SuppressFinalize(this);
                }
            }

            protected virtual async ValueTask DisposeAsyncCore()
            {
                // Perform actual async disposal logic here
                await Task.CompletedTask; // Replace with actual async cleanup if needed
            }

            ~RelationalDataReader()
            {
                Dispose(false);
            }
        }

        private async ValueTask<RelationalDataReader> AwaitUsingFunc()
        {
            await Task.Yield();
            return new RelationalDataReader();
        }

        private async Task Foo()
        {
            await Task.Yield();

            try
            {
                await Task.Yield();
                CaptureAndThrow();
            }
            finally
            {
                await Task.Yield();
            }
        }

        void CaptureAndThrow()
        {
            try
            {
                Bar();
            }
            catch (Exception e)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e).Throw();
            }
        }

        private void Bar()
        {
            try
            {
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                Console.WriteLine(nameof(RunAsync));
                throw;
            }
        }
    }
}

#endif
