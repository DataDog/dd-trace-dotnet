using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Coordinator
{
    public abstract class IntegrationSampleBase<TEnum> where TEnum : struct, IConvertible, IComparable, IFormattable
    {
        protected ManualResetEventSlim _bootstrappedEvent = new ManualResetEventSlim(initialState: false);
        protected ManualResetEventSlim _seededEvent = new ManualResetEventSlim(initialState: false);

        public IntegrationSampleBase()
        {
            BootstrapDependendencies();
            SeedData();
            RegisterParts();
        }

        public bool IsBootstrapped { get; set; }
        public bool IsSeeded { get; set; }

        public ConcurrentDictionary<TEnum, SamplePart> Parts { get; } = new ConcurrentDictionary<TEnum, SamplePart>();

        public abstract void BootstrapDependendencies();
        public abstract void SeedData();
        public abstract void RegisterParts();

        private SamplePart Get(TEnum partType)
        {
            if (!Parts.TryGetValue(partType, out var part))
            {
                throw new ArgumentException($"There is no part of type {partType} within {this.GetType().Name}");
            }

            _bootstrappedEvent.Wait();
            _seededEvent.Wait();

            return part;
        }

        public void Run(TEnum partType)
        {
            var part = Get(partType);
            part.Delegate();
        }

        public async Task RunAsync(TEnum partType)
        {
            var part = Get(partType);
            await part.AsyncDelegate();
        }

        public class SamplePart
        {
            public TEnum PartType { get; set; }
            public Action Delegate { get; set; }
            public Func<Task> AsyncDelegate { get; set; }
        }
    }
}
