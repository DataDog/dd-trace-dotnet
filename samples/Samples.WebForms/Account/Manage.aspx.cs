using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;

namespace Samples.WebForms.Account
{
    public partial class Manage : Page
    {
        protected string SuccessMessage { get; private set; }

        protected bool CanRemoveExternalLogins { get; private set; }

        private bool HasPassword(ApplicationUserManager manager)
        {
            var user = manager.FindById(User.Identity.GetUserId());

            return (user?.PasswordHash != null);
        }

        protected void Page_Load()
        {
            if (!IsPostBack)
            {
                var manager = Context.GetOwinContext().GetUserManager<ApplicationUserManager>();

                if (HasPassword(manager))
                {
                    changePasswordHolder.Visible = true;
                }
                else
                {
                    setPassword.Visible = true;
                    changePasswordHolder.Visible = false;
                }

                CanRemoveExternalLogins = manager.GetLogins(User.Identity.GetUserId()).Count() > 1;

                // Render success message
                var message = Request.QueryString["m"];

                if (message != null)
                {
                    // Strip the query string from action
                    Form.Action = ResolveUrl("~/Account/Manage");

                    SuccessMessage =
                        message == "ChangePwdSuccess"
                            ? "Your password has been changed."
                            : message == "SetPwdSuccess"
                                ? "Your password has been set."
                                : message == "RemoveLoginSuccess"
                                    ? "The account was removed."
                                    : string.Empty;

                    successMessage.Visible = !string.IsNullOrEmpty(SuccessMessage);
                }
            }
        }

        protected void ChangePassword_Click(object sender, EventArgs e)
        {
            if (IsValid)
            {
                var manager = new ApplicationUserManager(new CoffeehouseApiUserStore());

                var result = manager.ChangePassword(User.Identity.GetUserId(), CurrentPassword.Text, NewPassword.Text);

                if (result.Succeeded)
                {
                    var user = manager.FindById(User.Identity.GetUserId());
                    IdentityHelper.SignIn(manager, user, isPersistent: false);
                    Response.Redirect("~/Account/Manage?m=ChangePwdSuccess");
                }
                else
                {
                    AddErrors(result);
                }
            }
        }

        protected void SetPassword_Click(object sender, EventArgs e)
        {
            if (IsValid)
            {
                var manager = new ApplicationUserManager(new CoffeehouseApiUserStore());

                var result = manager.AddPassword(User.Identity.GetUserId(), password.Text);

                if (result.Succeeded)
                {
                    Response.Redirect("~/Account/Manage?m=SetPwdSuccess");
                }
                else
                {
                    AddErrors(result);
                }
            }
        }

        public IEnumerable<UserLoginInfo> GetLogins()
        {
            var manager = new ApplicationUserManager(new CoffeehouseApiUserStore());

            var accounts = manager.GetLogins(User.Identity.GetUserId());

            CanRemoveExternalLogins = accounts.Count() > 1 || HasPassword(manager);

            return accounts;
        }

        public void RemoveLogin(string loginProvider, string providerKey)
        {
            var manager = new ApplicationUserManager(new CoffeehouseApiUserStore());

            var result = manager.RemoveLogin(User.Identity.GetUserId(), new UserLoginInfo(loginProvider, providerKey));
            var msg = string.Empty;

            if (result.Succeeded)
            {
                var user = manager.FindById(User.Identity.GetUserId());
                IdentityHelper.SignIn(manager, user, isPersistent: false);
                msg = "?m=RemoveLoginSuccess";
            }

            Response.Redirect("~/Account/Manage" + msg);
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }
    }
}
