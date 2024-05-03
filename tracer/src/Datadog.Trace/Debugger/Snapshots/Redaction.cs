// <copyright file="Redaction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal enum RedactionReason
    {
        None,
        Identifier,
        Type
    }

    internal static class Redaction
    {
        private static readonly Type[] AllowedCollectionTypes =
        {
            typeof(List<>),
            typeof(ArrayList),
            typeof(LinkedList<>),
            typeof(SortedList),
            typeof(SortedList<,>),
            typeof(Stack),
            typeof(Stack<>),
            typeof(ConcurrentStack<>),
            typeof(Queue),
            typeof(Queue<>),
            typeof(ConcurrentQueue<>),
            typeof(HashSet<>),
            typeof(SortedSet<>),
            typeof(ConcurrentBag<>),
            typeof(BlockingCollection<>),
            typeof(ConditionalWeakTable<,>),
        };

        private static readonly Type[] AllowedDictionaryTypes =
        {
            typeof(Dictionary<,>),
            typeof(SortedDictionary<,>),
            typeof(ConcurrentDictionary<,>),
            typeof(Hashtable),
        };

        private static readonly string[] AllowedSpecialCasedCollectionTypeNames = { }; // "RangeIterator"

        internal static readonly Type[] AllowedTypesSafeToCallToString =
        {
            typeof(DateTime),
            typeof(TimeSpan),
            typeof(DateTimeOffset),
            typeof(Uri),
            typeof(Guid),
            typeof(Version),
            typeof(StackTrace),
            typeof(StringBuilder)
        };

        private static readonly Type[] DeniedTypes =
        {
            typeof(SecureString),
        };

        private static readonly Trie TypeTrie = new();
        private static readonly HashSet<string> RedactedTypes = new();

        private static HashSet<string> _redactKeywords = new()
        {
            "2fa",
            "accesstoken",
            "address",
            "aiohttpsession",
            "apikey",
            "appkey",
            "apisecret",
            "apisignature",
            "applicationkey",
            "auth",
            "authorization",
            "authtoken",
            "ccnumber",
            "certificatepin",
            "cipher",
            "clientid",
            "clientsecret",
            "config",
            "connectionstring",
            "connectsid",
            "cookie",
            "credentials",
            "creditcard",
            "csrf",
            "csrftoken",
            "cvv",
            "databaseurl",
            "dburl",
            "encryptionkey",
            "encryptionkeyid",
            "env",
            "geolocation",
            "gpgkey",
            "ipaddress",
            "jti",
            "jwt",
            "licensekey",
            "masterkey",
            "mysqlpwd",
            "nonce",
            "oauth",
            "oauthtoken",
            "otp",
            "passhash",
            "passwd",
            "password",
            "passwordb",
            "pemfile",
            "pgpkey",
            "phpsessid",
            "pin",
            "pincode",
            "pkcs8",
            "privatekey",
            "publickey",
            "pwd",
            "recaptchakey",
            "refreshtoken",
            "routingnumber",
            "salt",
            "secret",
            "secretkey",
            "secrettoken",
            "securityanswer",
            "securitycode",
            "securityquestion",
            "serviceaccountcredentials",
            "session",
            "sessionid",
            "sessionkey",
            "setcookie",
            "signature",
            "signaturekey",
            "sshkey",
            "ssn",
            "symfony",
            "token",
            "transactionid",
            "twiliotoken",
            "usersession",
            "voterid",
            "xapikey",
            "xauthtoken",
            "xcsrftoken",
            "xforwardedfor",
            "xrealip",
            "xsrf",
            "xsrftoken"
        };

        internal static bool IsSafeToCallToString(Type type)
        {
            return TypeExtensions.IsSimple(type) ||
                   AllowedTypesSafeToCallToString.Contains(type) ||
                   IsSupportedCollection(type);
        }

        internal static bool IsSupportedDictionary(object o)
        {
            if (o == null)
            {
                return false;
            }

            var type = o.GetType();
            return AllowedDictionaryTypes.Any(whiteType => whiteType == type || (type.IsGenericType && whiteType == type.GetGenericTypeDefinition()));
        }

        internal static bool IsSupportedCollection(object o)
        {
            if (o == null)
            {
                return false;
            }

            return IsSupportedCollection(o.GetType());
        }

        internal static bool IsSupportedCollection(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (type.IsArray)
            {
                return true;
            }

            return AllowedCollectionTypes.Any(whiteType => whiteType == type || (type.IsGenericType && whiteType == type.GetGenericTypeDefinition())) ||
                   AllowedSpecialCasedCollectionTypeNames.Any(white => white.Equals(type.Name, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsRedactedType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            var shouldRedact = DeniedTypes.Any(deniedType => deniedType == type || (type.IsGenericType && deniedType == type.GetGenericTypeDefinition()));

            if (shouldRedact)
            {
                return true;
            }

            var typeFullName = type.FullName;

            if (string.IsNullOrEmpty(typeFullName))
            {
                return false;
            }

            if (TypeTrie.HasMatchingPrefix(typeFullName))
            {
                var stringStartsWith = TypeTrie.GetStringStartingWith(typeFullName);
                return string.IsNullOrEmpty(stringStartsWith) || stringStartsWith.Length == typeFullName.Length;
            }

            return RedactedTypes.Contains(typeFullName);
        }

        public static bool IsRedactedKeyword(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            name = Normalize(name);
            return _redactKeywords.Contains(name);
        }

        public static bool ShouldRedact(string name, Type type, out RedactionReason redactionReason)
        {
            if (IsRedactedKeyword(name))
            {
                redactionReason = RedactionReason.Identifier;
                return true;
            }

            if (IsRedactedType(type))
            {
                redactionReason = RedactionReason.Type;
                return true;
            }

            redactionReason = RedactionReason.None;
            return false;
        }

        private static string Normalize(string name)
        {
            StringBuilder sb = null;
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                var isUpper = char.IsUpper(c);
                var isRemovable = IsRemovableChar(c);
                if (isUpper || isRemovable || sb != null)
                {
                    sb ??= new StringBuilder(name.Substring(0, i));

                    if (isUpper)
                    {
                        sb.Append(char.ToLower(c));
                    }
                    else if (!isRemovable)
                    {
                        sb.Append(c);
                    }
                }
            }

            return sb != null ? sb.ToString() : name;
        }

        private static bool IsRemovableChar(char c)
        {
            return c == '_' || c == '-' || c == '$' || c == '@';
        }

        public static void SetConfig(DebuggerSettings settings)
        {
            foreach (var identifier in settings.RedactedIdentifiers)
            {
                _redactKeywords.Add(Normalize(identifier.Trim()));
            }

            foreach (var type in settings.RedactedTypes)
            {
                if (type.EndsWith("*"))
                {
                    var newTypeName = type.Substring(0, type.Length - 1);
                    TypeTrie.Insert(newTypeName);
                }
                else
                {
                    RedactedTypes.Add(type);
                }
            }
        }
    }
}
