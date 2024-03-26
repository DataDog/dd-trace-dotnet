// <copyright file="LdapTokenizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;

#nullable enable

namespace Datadog.Trace.Iast.SensitiveData;

/// <summary>
/// Tokenizer for LDAP_INJECTION vulnerability
/// It locates all literals in a LDAP query, which may be multiple (las term after '=')
/// ((objectCategory=group)(member=CN=Jon Brion,OU=Employees,DC=theitbros,DC=com)) -> ((objectCategory = group)(member = CN =?, OU =?, DC =?, DC =?))
/// </summary>
internal class LdapTokenizer : ITokenizer
{
    private const string _ldapPattern = @"\(.*?(?:~=|=|<=|>=)(?<LITERAL>[^)]+)\)";
    private Regex _ldapRegex;

    public LdapTokenizer(TimeSpan timeout)
    {
        _ldapRegex = new Regex(_ldapPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, timeout);
    }

    public List<Range> GetTokens(Evidence evidence, IntegrationId? integrationId = null)
    {
        var value = evidence.Value;
        if (value is null) { return []; }

        var res = new List<Range>(5);
        foreach (Match? match in _ldapRegex.Matches(value))
        {
            if (match != null && match.Success)
            {
                var group = match.Groups["LITERAL"];
                if (group != null && group.Success)
                {
                    int start = group.Index;
                    int end = group.Index + group.Length;

                    res.Add(new Range(start, end - start, null));
                }
            }
        }

        return res;
    }
}
