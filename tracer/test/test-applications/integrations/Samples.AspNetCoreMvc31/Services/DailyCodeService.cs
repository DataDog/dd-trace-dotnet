using System;
using WebService.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace WebService.Services
{
  public class DailyCodeService
  {
    public Course Course { get; }

    public DailyCodeService(Course course)
    {
      Course = course;
    }

    public string GenerateCodeForDate(DateTime date, string role)
    {
      var length = 6;

      if (role == "admin")
      {
        length = 10;
      }
      else if (role == "teacher")
      {
        length = 8;
      }

      if (Course.Code.Contains("001"))
      {
        length -= 1;
      }

      var seed = Course.Name.GetHashCode() ^ role.GetHashCode();

      var code = CreateDailyCodeCandidate(seed, date, length);

      var iterations = 0;

      while (!CodeHardEnough(code))
      {
        if (iterations > 10)
        {
          throw new Exception("Failed to create daily code");
        }

        iterations += 1;
        seed = seed * 2 + iterations;
        code = CreateDailyCodeCandidate(seed, date, length);
        code = RemoveDuplicateDigits(code);
      }

      return code;
    }

    private string RemoveDuplicateDigits(string code)
    {
      var fixedCode = "";

      for (var i = 0; i < code.Length-1; i++)
      {
        if (code[i] == code[i + 1])
        {
          fixedCode += (int.Parse(""+code[i])+1 % 10).ToString();
        } else {
          fixedCode += code[i];
        }
      }
      //adding the last digit, when we can be sure it not as it previous digit.
      fixedCode += code.Last();

      return fixedCode;
    }

    private string CreateDailyCodeCandidate(int seed, DateTime date, int length)
    {
      var hash = Math.Abs(seed * 39 ^ date.GetHashCode());

      var code = hash.ToString("D" + length);

      if (code.Length < length)
      {
        code = code + string.Join("", code.Reverse());
      }

      if (code.Length > length)
      {
        code = code.Substring(0, length);
      }

      return code;
    }

    private bool CodeHardEnough(string code)
    {
      for (var i = 0; i < code.Length - 1; i++)
      {
        if (code[i] == code[i + 1])
        {
          return false;
        }
      }

      var digitsCount = code.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

      foreach (var kv in digitsCount)
      {
        if (kv.Value > 2)
        {
          return false;
        }
      }

      return true;
    }
  }
}
