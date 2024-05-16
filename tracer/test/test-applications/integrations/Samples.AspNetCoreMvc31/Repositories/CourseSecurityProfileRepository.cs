using WebService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebService.Repositories
{
    public static class CourseSecurityProfileRepository
    {
        static CourseSecurityProfileRepository()
        {
        }

        public static async Task<SecurityProfile> GetSecurityProfile(Course course)
        {
            switch (course.Code)
            {
                case "AEA-400":
                    return new SecurityProfile
                    {
                        MinimumLength = 12,
                        MinimumLengthOfSecurityAnswer = 5,
                        MinimumNumberOfSecurityQuestions = 3,
                        RequireLowerAndUpperLetters = true,
                        RequireNumbersAndLetters = true,
                        RequireSpecialChars = true
                    };
                default:
                    return null;
            }
        }
    }
}
