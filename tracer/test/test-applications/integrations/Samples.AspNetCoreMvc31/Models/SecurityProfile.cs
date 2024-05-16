using System;
using System.Collections.Generic;
using System.Text;

namespace WebService.Models
{
    public class SecurityProfile
    {
        public bool RequireNumbersAndLetters { get; set; }

        public bool RequireSpecialChars { get; set; }

        public bool RequireLowerAndUpperLetters { get; set; }

        public int MinimumLength { get; set; }

        public int MinimumNumberOfSecurityQuestions { get; set; }

        public int MinimumLengthOfSecurityAnswer { get; set; }
    }
}
