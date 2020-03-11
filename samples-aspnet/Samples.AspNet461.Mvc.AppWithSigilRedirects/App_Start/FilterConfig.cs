using System.Web;
using System.Web.Mvc;

namespace Samples.AspNet461.Mvc.AppWithSigilRedirects
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
