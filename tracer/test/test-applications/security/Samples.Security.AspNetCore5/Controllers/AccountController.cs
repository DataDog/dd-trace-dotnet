using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Samples.Security.AspNetCore5.Models;
using Samples.Security.AspNetCore5.IdentityStores;

namespace Samples.Security.AspNetCore5.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserStore<IdentityUser> _userStore;

    public AccountController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager, IUserStore<IdentityUser> userStore)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _userStore = userStore;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (User.Identity.IsAuthenticated)
        {
            return Content($"Logged in as{User.Identity.Name}");
        }

        return View(new LoginModel { Input = new LoginModel.InputModel { UserName = "TestUser", Password = "test" } });
    }

    [HttpPost]
    public IActionResult LogOut()
    {
        _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity.IsAuthenticated)
        {
            return Content("Logged in as" + User.Identity.Name);
        }

        return View(new RegisterModel());
    }

    [HttpGet]
    [Route("account/reset-memory-db")]
    public IActionResult ResetMemoryDb()
    {
        UserStoreMemory.ResetUsers();
        return Content("ok");
    }

    [HttpPost]
    public async Task<IActionResult> Index(LoginModel model, string returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (ModelState.IsValid)
        {
            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            var result = await _signInManager.PasswordSignInAsync(model.Input.UserName, model.Input.Password, model.Input.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                return LocalRedirect(returnUrl);
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = model.Input.RememberMe });
            }

            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return RedirectToAction(nameof(Register));
        }

        // If we got this far, something failed, redisplay form
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterModel model, string returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        if (ModelState.IsValid)
        {
            var user = new IdentityUser { UserName = model.Input.UserName, Email = model.Input.Email };
            var result = await _userManager.CreateAsync(user, model.Input.Password);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        // If we got this far, something failed, redisplay form
        return Content("Registered and logged in as" + User.Identity.Name);
    }
}
