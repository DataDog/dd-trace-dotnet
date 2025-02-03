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
using Datadog.Trace.Logging;
using TypeExtensions = Datadog.Trace.Debugger.Helpers.TypeExtensions;

#nullable enable

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
        private const int MaxStackAlloc = 512;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Redaction));

        private static readonly Type[] AllowedCollectionTypes =
        [
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
            typeof(ConditionalWeakTable<,>)
        ];

        private static readonly Type[] AllowedDictionaryTypes =
        [
            typeof(Dictionary<,>),
            typeof(SortedDictionary<,>),
            typeof(ConcurrentDictionary<,>),
            typeof(Hashtable)
        ];

        private static readonly string[] AllowedSpecialCasedCollectionTypeNames = []; // "RangeIterator"

        internal static readonly Type[] AllowedTypesSafeToCallToString =
        [
            typeof(DateTime),
            typeof(TimeSpan),
            typeof(DateTimeOffset),
            typeof(Uri),
            typeof(Guid),
            typeof(Version),
            typeof(StackTrace),
            typeof(StringBuilder)
        ];

        private static readonly Type[] DeniedTypes =
        [
            typeof(SecureString)
        ];

        private static readonly Trie TypeTrie = new();

        private static readonly HashSet<string> RedactedTypes = [];

        private static readonly HashSet<string> RedactKeywords =
        [
            "2fa",
            "accesstoken",
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
        ];

        internal static bool IsSafeToCallToString(Type type)
        {
            return TypeExtensions.IsSimple(type) ||
                   AllowedTypesSafeToCallToString.Contains(type) ||
                   IsSupportedCollection(type);
        }

        internal static bool IsSupportedDictionary(object? o)
        {
            if (o == null)
            {
                return false;
            }

            var type = o.GetType();
            return AllowedDictionaryTypes.Any(whiteType => whiteType == type || (type.IsGenericType && whiteType == type.GetGenericTypeDefinition()));
        }

        internal static bool IsSupportedCollection(object? o)
        {
            if (o == null)
            {
                return false;
            }

            return IsSupportedCollection(o.GetType());
        }

        internal static bool IsSupportedCollection(Type? type)
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

        internal static bool IsRedactedType(Type? type)
        {
            if (type == null)
            {
                return false;
            }

            Type? genericDefinition = null;
            if (type.IsGenericType)
            {
                genericDefinition = type.GetGenericTypeDefinition();
            }

            foreach (var deniedType in DeniedTypes)
            {
                if (deniedType == type ||
                    (genericDefinition != null && deniedType == genericDefinition))
                {
                    return true;
                }
            }

            var typeFullName = type.FullName;

            if (string.IsNullOrEmpty(typeFullName))
            {
                return false;
            }

            if (RedactedTypes.Contains(typeFullName))
            {
                return true;
            }

            if (TypeTrie.HasMatchingPrefix(typeFullName))
            {
                var stringStartsWith = TypeTrie.GetStringStartingWith(typeFullName);
                return string.IsNullOrEmpty(stringStartsWith) || stringStartsWith.Length == typeFullName.Length;
            }

            return false;
        }

        internal static bool IsRedactedKeyword(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return !TryNormalize(name, out var result) || RedactKeywords.Contains(result);
        }

        internal static bool ShouldRedact(string name, Type type, out RedactionReason redactionReason)
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

        private static unsafe bool TryNormalize(string identifier, out string result)
        {
            result = identifier;
            if (string.IsNullOrEmpty(identifier))
            {
                return true;
            }

            if (identifier.Length > MaxStackAlloc)
            {
                Log.Error("Identifier length {Length} exceeds maximum allowed length of {MaxSize}, hence we are going to redact this identifier", identifier.Length, property1: MaxStackAlloc);
                return false;
            }

            bool needsNormalization = false;
            var length = identifier.Length;
            for (int i = 0; i < length && !needsNormalization; i++)
            {
                char c = identifier[i];
                needsNormalization = char.IsUpper(c) || IsRemovableChar(c);
            }

            if (!needsNormalization)
            {
                return true;
            }

            int written = 0;
            var buffer = stackalloc char[length];
            for (int i = 0; i < identifier.Length; i++)
            {
                char c = identifier[i];
                if (char.IsUpper(c))
                {
                    buffer[written++] = char.ToLowerInvariant(c);
                }
                else if (!IsRemovableChar(c))
                {
                    buffer[written++] = c;
                }
            }

            result = new string(buffer, 0, written);
            return true;
        }

        private static bool IsRemovableChar(char c)
        {
            return c is '_' or '-' or '$' or '@';
        }

        internal static void SetConfig(HashSet<string> redactedIdentifiers, HashSet<string> redactedExcludedIdentifiers, HashSet<string> redactedTypes)
        {
#if NET6_0_OR_GREATER
            RedactKeywords.EnsureCapacity(RedactKeywords.Count + redactedIdentifiers.Count);
#endif
            foreach (var identifier in redactedIdentifiers)
            {
                if (TryNormalize(identifier, out var result))
                {
                    RedactKeywords.Add(result);
                }
                else
                {
                    Log.Error("Skipping identifier that exceeds maximum length: {Length}", property: identifier.Length);
                }
            }

            foreach (var excluded in redactedExcludedIdentifiers)
            {
                if (TryNormalize(excluded, out var result))
                {
                    RedactKeywords.Remove(result);
                }
                else
                {
                    Log.Error("Skipping excluded identifier that exceeds maximum length: {Length}", property: excluded.Length);
                }
            }

            foreach (var type in redactedTypes)
            {
                if (type.EndsWith("*"))
                {
#if NETCOREAPP3_1_OR_GREATER
                    TypeTrie.Insert(type[..^1]);
#else
                    var newTypeName = type.Substring(0, type.Length - 1);
                    TypeTrie.Insert(newTypeName);
#endif
                }
                else
                {
                    RedactedTypes.Add(type);
                }
            }
        }
    }
}
