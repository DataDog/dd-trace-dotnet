using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace Datadog.Tracer
{
    internal static class AsyncBufferExtension
    {
        public static IObservable<IList<T>> AsyncBuffer<T>(this IObservable<T> source, int batchSize, TimeSpan period, int boundedCapacity)
        {
            return new AsyncBuffer<T>(source, batchSize, period, boundedCapacity);
        }
    }

    internal class AsyncBuffer<T> : IObservable<IList<T>>
    {
        private BatchBlock<T> _batchBlock;
        private TransformBlock<T[], T[]> _timeoutTransformBlock;
        private IObservable<IList<T>> _observable;
        Timer _triggerBatchTimer;

        public AsyncBuffer(IObservable<T> source, int batchSize, TimeSpan period, int boundedCapacity)
        {
            // Based on https://stackoverflow.com/a/9423830
            // Creates a BatchBlock triggered by a timer with a TransformBlock acting as a debouncer
            _batchBlock = new BatchBlock<T>(batchSize, new GroupingDataflowBlockOptions { BoundedCapacity = boundedCapacity });
            _triggerBatchTimer = new Timer((s) => _batchBlock.TriggerBatch());
            _timeoutTransformBlock = new TransformBlock<T[], T[]>((value) =>
            {
                _triggerBatchTimer.Change(period, TimeSpan.FromMilliseconds(-1));
                return value;
            });
            _triggerBatchTimer.Change(period, TimeSpan.FromMilliseconds(-1));
            _batchBlock.LinkTo(_timeoutTransformBlock);

            _observable = _timeoutTransformBlock.AsObservable();
            // _batchBlock.Post will return false if the message was dropped
            // TODO:bertrand warn if we're dropping messages
            source.Subscribe(x => _batchBlock.Post(x));
        }

        public IDisposable Subscribe(IObserver<IList<T>> observer)
        {
            return _observable.Subscribe(observer);
        }
    }
}
