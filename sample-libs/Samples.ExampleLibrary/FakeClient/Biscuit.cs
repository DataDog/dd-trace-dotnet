using System;
using System.Collections.Generic;

namespace Samples.ExampleLibrary.FakeClient
{
    public class Biscuit<T> : Biscuit
    {
        public T Reward { get; set; }
    }

    public class Biscuit
    {
        public Guid Id { get; set; }
        public string Message { get; set; }
        public List<object> Treats { get; set; } = new List<object>();
    }
}
