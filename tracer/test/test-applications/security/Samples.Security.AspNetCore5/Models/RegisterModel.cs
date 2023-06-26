using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;

namespace Samples.Security.AspNetCore5.Models
{
    public class RegisterModel
    {
        public RegisterModel()
        {
            
        }
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }


        public class InputModel
        {
            [Required]
            [Display(Name = "UserName")]
            public string UserName { get; set; }
            
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 4)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }
    }
}
