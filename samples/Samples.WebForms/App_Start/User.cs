using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace Samples.WebForms
{
    public class User : IdentityUser
    {
        public string CompanyId { get; set; }

        public string Name
        {
            get { return UserName; }
            set { UserName = value; }
        }

        public ClaimsIdentity GenerateUserIdentity(ApplicationUserManager manager)
        {
            var userIdentity = manager.CreateIdentity(this, DefaultAuthenticationTypes.ApplicationCookie);

            return userIdentity;
        }

        public Task<ClaimsIdentity> GenerateUserIdentityAsync(ApplicationUserManager manager)
            => Task.FromResult(GenerateUserIdentity(manager));
    }
}
