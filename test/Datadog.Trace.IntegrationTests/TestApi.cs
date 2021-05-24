using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using MsgPack;

namespace Datadog.Trace.IntegrationTests
{
    internal class TestApi : IApi
    {
        private ManualResetEventSlim _resetEvent = new ManualResetEventSlim();
        private List<MessagePackObject> _objects = null;

        public List<MessagePackObject> Wait()
        {
            _resetEvent.Wait();
            var objects = Interlocked.Exchange(ref _objects, null);
            _resetEvent.Reset();
            return objects;
        }

        public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces)
        {
            var packObject = Unpacking.UnpackObject(traces.ToArray()).Value.AsList();
            if (packObject.Count > 0)
            {
                var previous = Interlocked.Exchange(ref _objects, null);
                if (previous is not null)
                {
                    Interlocked.Exchange(ref _objects, previous.Concat(packObject.ToList()).ToList());
                }
                else
                {
                    Interlocked.Exchange(ref _objects, packObject.ToList());
                }

                _resetEvent.Set();
            }

            return Task.FromResult(true);
        }
    }
}
