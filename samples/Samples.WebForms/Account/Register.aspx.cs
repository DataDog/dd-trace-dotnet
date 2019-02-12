using System;
using System.Linq;
using System.Web.UI;

namespace Samples.WebForms.Account
{
    public partial class Register : Page
    {
        protected void CreateUser_Click(object sender, EventArgs e)
        {
            var manager = new ApplicationUserManager(new CoffeehouseApiUserStore());

            var user = new User
                       {
                           Id = UserName.Text,
                           UserName = UserName.Text,
                           CompanyId = Guid.NewGuid().ToString("N"),
                           Email = UserName.Text + "@datadogdemocoffeehouseappwebforms.com"
                       };

            try
            {
                var result = manager.CreateAsync(user, Password.Text).GetAwaiter().GetResult();

                if (result.Succeeded)
                {
                    IdentityHelper.SignIn(manager, user, isPersistent: false);
                    IdentityHelper.RedirectToReturnUrl(Request.QueryString["ReturnUrl"], Response);
                }
                else
                {
                    ErrorMessage.Text = result.Errors.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage.Text = ex.Message;
            }
        }
    }
}
