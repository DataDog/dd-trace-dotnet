using System.Collections.Generic;

namespace Samples.WebForms.Ninject.Services
{
    public class InMemoryDataRepository : IDataRepository
    {
        public List<string> GetItems()
        {
            return new List<string>
            {
                "Item 1",
                "Item 2",
                "Item 3"
            };
        }
    }
}
