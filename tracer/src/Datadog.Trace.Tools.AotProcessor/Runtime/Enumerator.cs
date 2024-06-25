using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Datadog.Trace.Tools.AotProcessor.Interfaces;

namespace Datadog.Trace.Tools.AotProcessor.Runtime
{
    internal unsafe class Enumerator<T, TOut> : IEnumerator
        where TOut : unmanaged
    {
        private readonly T[] items;
        private uint index = 0;
        private Func<T, TOut> convert;

        public Enumerator(T[] items, Func<T, TOut> convert)
        {
            this.items = items;
            this.convert = convert;
        }

        public void Dispose()
        {
            Reset(0);
        }

        public bool Reset(uint pos)
        {
            if (pos < items.Length)
            {
                index = pos;
                return true;
            }

            return false;
        }

        public uint Delivered
        {
            get => index;
        }

        public unsafe uint Fetch(TOut* rTypeRefs, uint cMax)
        {
            uint count = 0;
            while (count < cMax && index < items.Length)
            {
                rTypeRefs[count++] = convert(items[index++]);
            }

            return count;
        }
    }

    internal interface IEnumerator : IDisposable
    {
        bool Reset(uint pos);

        uint Delivered { get; }
    }
}
