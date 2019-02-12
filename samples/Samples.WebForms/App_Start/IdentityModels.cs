using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security;

namespace Samples.WebForms
{
    public static class IdentityHelper
    {
        public const string XsrfKey = "XsrfId";

        public static void SignIn(ApplicationUserManager manager, User user, bool isPersistent)
        {
            var authenticationManager = HttpContext.Current.GetOwinContext().Authentication;

            authenticationManager.SignOut(DefaultAuthenticationTypes.ExternalCookie);

            var identity = manager.CreateIdentity(user, DefaultAuthenticationTypes.ApplicationCookie);

            authenticationManager.SignIn(
                                         new AuthenticationProperties
                                         {
                                             IsPersistent = isPersistent
                                         },
                                         identity);
        }

        public const string ProviderNameKey = "providerName";

        public static string GetProviderNameFromRequest(HttpRequest request) => request[ProviderNameKey];

        public static string GetExternalLoginRedirectUrl(string accountProvider) => "/Account/RegisterExternalLogin?" + ProviderNameKey + "=" + accountProvider;

        private static bool IsLocalUrl(string url) => !string.IsNullOrEmpty(url) && ((url[index: 0] == '/' && (url.Length == 1 || (url[index: 1] != '/' && url[index: 1] != '\\'))) || (url.Length > 1 && url[index: 0] == '~' && url[index: 1] == '/'));

        public static void RedirectToReturnUrl(string returnUrl, HttpResponse response)
        {
            if (!string.IsNullOrEmpty(returnUrl) && IsLocalUrl(returnUrl))
            {
                response.Redirect(returnUrl);
            }
            else
            {
                response.Redirect("~/");
            }
        }
    }

    public class ApplicationUserManager : UserManager<User>
    {
        public ApplicationUserManager(IUserStore<User> store)
            : base(store)
        {
        }

        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context)
        {
            var manager = new ApplicationUserManager(new CoffeehouseApiUserStore());

            var dataProtectionProvider = options.DataProtectionProvider;

            if (dataProtectionProvider != null)
            {
                manager.UserTokenProvider = new DataProtectorTokenProvider<User>(dataProtectionProvider.Create("ASP.NET Identity"));
            }

            return manager;
        }
    }

    public class ApplicationSignInManager : SignInManager<User, string>
    {
        public ApplicationSignInManager(ApplicationUserManager userManager, IAuthenticationManager authenticationManager)
            :
            base(userManager, authenticationManager)
        {
        }

        public override Task<ClaimsIdentity> CreateUserIdentityAsync(User user)
            => user.GenerateUserIdentityAsync((ApplicationUserManager)UserManager);

        public static ApplicationSignInManager Create(IdentityFactoryOptions<ApplicationSignInManager> options, IOwinContext context)
            => new ApplicationSignInManager(context.GetUserManager<ApplicationUserManager>(), context.Authentication);
    }
}
