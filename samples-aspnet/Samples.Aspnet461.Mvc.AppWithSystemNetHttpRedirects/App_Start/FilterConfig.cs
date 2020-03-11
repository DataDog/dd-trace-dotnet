using System.Web;
using System.Web.Mvc;

namespace Samples.Aspnet461.Mvc.AppWithSystemNetHttpRedirects
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
