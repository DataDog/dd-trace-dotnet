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

        private static HashSet<string> _redactKeywords = new()
        {
            "_csrf",
            "_csrf_token",
            "_session",
            "_xsrf",
            "2fa",
            "access_token",
            "address",
            "aiohttp_session",
            "api_key",
            "api_secret",
            "api_signature",
            "apikey",
            "auth",
            "auth_token",
            "authorization",
            "bank_account_number",
            "birthdate",
            "cc_number",
            "certificate_pin",
            "cipher",
            "client_id",
            "client_secret",
            "config",
            "connect.sid",
            "cookie",
            "credentials",
            "creditcard",
            "csrf",
            "csrf_token",
            "csrftoken",
            "cvv",
            "database_url",
            "db_url",
            "driver_license",
            "email",
            "encryption_key",
            "encryption_key_id",
            "env",
            "geo_location",
            "gpg_key",
            "gpgkey",
            "ip_address",
            "jti",
            "jwt",
            "license_key",
            "licenseplate",
            "maidenname",
            "mailaddress",
            "master_key",
            "mysql_pwd",
            "nonce",
            "oauth",
            "oauth_token",
            "otp",
            "passhash",
            "passport",
            "passport_number",
            "passportno",
            "passportnum",
            "passwd",
            "password",
            "passwordb",
            "pem_file",
            "pgp_key",
            "pgpkey",
            "phone",
            "phoneno",
            "phonenum",
            "phonenumber",
            "PHPSESSID",
            "pin",
            "pincode",
            "pkcs8",
            "plateno",
            "platenum",
            "platenumber",
            "private_key",
            "privatekey",
            "province",
            "public_key",
            "recaptcha_key",
            "refresh_token",
            "remote_addr",
            "routing_number",
            "salt",
            "secret",
            "secret_token",
            "secretKey",
            "security_answer",
            "security_code",
            "security_question",
            "service_account_credentials",
            "session",
            "session_key",
            "sessionid",
            "set_cookie",
            "signature",
            "signature_key",
            "ssh_key",
            "sshkey",
            "ssn",
            "streetaddress",
            "surname",
            "symfony",
            "tax_identification_number",
            "telephone",
            "token",
            "transaction_id",
            "twilio_token",
            "user_session",
            "voter_id",
            "x_api_key",
            "x_csrftoken",
            "x_forwarded_for",
            "x_real_ip",
            "x-auth-token",
            "XSRF-TOKEN",
            "zipcode",
            "pwd"
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

            return DeniedTypes.Any(deniedType => deniedType == type || (type.IsGenericType && deniedType == type.GetGenericTypeDefinition()));
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
            var redactedByIdentifier = IsRedactedKeyword(name);
            var redactedByType = IsRedactedType(type);
            var shouldRedact = redactedByIdentifier | redactedByType;

            redactionReason = RedactionReason.None;
            if (shouldRedact)
            {
                redactionReason = redactedByIdentifier ? RedactionReason.Identifier : RedactionReason.Type;
            }

            return shouldRedact;
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
        }
    }
}
