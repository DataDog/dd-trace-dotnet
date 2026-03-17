using System.Collections.Generic;

namespace Samples.WebForms.Ninject.Services
{
    public interface IDataRepository
    {
        List<string> GetItems();
    }
}
