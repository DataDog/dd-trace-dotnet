using System;
using System.Collections.Generic;
using System.Text;

namespace WebService.Models
{
    public class PasswordAuthenticationRequest
    {
        public string Email { get; set; }

        public string Password { get; set; }
    }
}
