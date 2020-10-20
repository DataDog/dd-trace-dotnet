using System.Collections.Generic;

namespace Samples.ExampleLibrary.GenericTests
{
    public struct StructContainer<T>
    {
        public List<T> Items { get; }

        public long Id { get; }

        public StructContainer(long id, List<T> items)
        {
            Id = id;
            Items = items;
        }
    }
}
