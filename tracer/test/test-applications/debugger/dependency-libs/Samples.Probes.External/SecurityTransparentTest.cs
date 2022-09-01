
using System;
using System.Diagnostics;
using System.Security;

[assembly: SecurityTransparent]
//[assembly: SecurityRules(SecurityRuleSet.Level2)]
namespace Samples.Probes.External
{
    /// <summary>
    /// For Tests
    /// </summary>
    public class SecurityTransparentTest
    {
        private readonly Random _randomizer = new Random();
    }
}
