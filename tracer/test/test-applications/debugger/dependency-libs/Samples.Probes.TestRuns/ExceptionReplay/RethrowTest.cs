using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

#if !NET461_OR_GREATER && !NETCOREAPP2_1

namespace Samples.Probes.TestRuns.ExceptionReplay
{
    [ExceptionReplayTestData(expectedNumberOfSnapshotsDefault: 5, expectedNumberOfSnaphotsFull: 22)]
    public class RethrowTest : IAsyncRun
    {
        private string _tempMethodName;
        
        public async Task RunAsync()
        {
            var methodName = nameof(RunAsync);
            
            await Task.Yield();

            try
            {
                try
                {
                    await Task.Yield();
                    throw new Exception("Bdika");
                }
                catch (Exception)
                {
                    await Task.Yield();
                    await Task.Yield();
                    await Task.Yield();
                    await RunAsync2();
                    throw;
                }
            }
            finally
            {
                await Task.Yield();
            }

#pragma warning disable CS0162 // Unreachable code detected
            _tempMethodName = methodName;
#pragma warning restore CS0162 // Unreachable code detected
        }

        public async Task RunAsync2()
        {
            var methodName = nameof(RunAsync2);
            
            await Task.Yield();

            try
            {
                try
                {
                    await Task.Yield();
                    await InTheMiddle();
                }
                catch (Exception)
                {
                    //await Task.Yield();
                    //await Task.Yield();
                    //await Task.Yield();
                    throw;
#pragma warning disable CS0162 // Unreachable code detected
                    await Task.Yield();
#pragma warning restore CS0162 // Unreachable code detected
                }
            }
            catch (Exception)
            {
                throw;
#pragma warning disable CS0162 // Unreachable code detected
                await Task.Yield();
#pragma warning restore CS0162 // Unreachable code detected
            }

            _tempMethodName = methodName;
        }

        public async Task InTheMiddle()
        {
            var methodName = nameof(InTheMiddle);
            
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

            _tempMethodName = methodName;
        }

        public async Task InTheMiddle2()
        {
            var methodName = nameof(InTheMiddle2);
            
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

            _tempMethodName = methodName;
        }

        public class RelationalDataReader : IAsyncDisposable, IDisposable
        {
            private bool _disposed = false;
#pragma warning disable CS0414 // Assigned but never used
            private bool _disposedAsync = false;
#pragma warning restore CS0414 // Assigned but never used

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
            var methodName = nameof(AwaitUsingFunc);
            await Task.Yield();
            _tempMethodName = methodName;
            return new RelationalDataReader();
        }

        private async Task Foo()
        {
            var methodName = nameof(Foo);
            
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

            _tempMethodName = methodName;
        }

        void CaptureAndThrow()
        {
            var methodName = nameof(CaptureAndThrow);
            
            try
            {
                RecursiveThrow();
            }
            catch (Exception e)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e).Throw();
            }

            _tempMethodName = methodName;
        }
        
        void RecursiveThrow(int depth = 5)
        {
            var methodName = nameof(RecursiveThrow);
            
            try
            {
                if (depth > 0)
                {
                    RecursiveThrow(depth - 1);    
                }
                else
                {
                    RecursiveCaptureAndThrow();   
                }
            }
            catch (Exception)
            {
                throw;
            }
            
            _tempMethodName = methodName;
        }
        
        void RecursiveCaptureAndThrow(int depth = 5)
        {
            var methodName = nameof(RecursiveCaptureAndThrow);
            
            try
            {
                if (depth > 0)
                {
                    RecursiveCaptureAndThrow(depth - 1);    
                }
                else
                {
                    Bar();   
                }
            }
            catch (Exception e)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e).Throw();
            }
            
            _tempMethodName = methodName;
        }
        
        private void Bar()
        {
            var methodName = nameof(Bar);
            
            try
            {
                throw new NotImplementedException();
            }
            catch (Exception)
            {
                Console.WriteLine(nameof(RunAsync));
                throw;
            }
            
#pragma warning disable CS0162 // Unreachable code detected
            _tempMethodName = methodName;
#pragma warning restore CS0162 // Unreachable code detected
        }
    }
}

#endif
