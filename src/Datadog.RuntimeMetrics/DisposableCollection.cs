using System;
using System.Collections.Generic;

namespace Datadog.RuntimeMetrics
{
    internal class DisposableCollection : IDisposable
    {
        private readonly IReadOnlyCollection<IDisposable> _collection;

        public DisposableCollection(params IDisposable[] disposables) : this((IReadOnlyCollection<IDisposable>)disposables)
        {
        }

        public DisposableCollection(IReadOnlyCollection<IDisposable> disposables)
        {
            _collection = disposables ?? throw new ArgumentNullException(nameof(disposables));
        }

        public void Dispose()
        {
            foreach (IDisposable disposable in _collection)
            {
                disposable?.Dispose();
            }
        }
    }
}
