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
using System.Threading;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;
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

    internal sealed class Redaction
    {
        // Same as the previous LFU cache capacity. Once full, results are computed without caching.
        internal const int MaxCachedTypes = 2048;

        internal const int MaxCachedKeywords = 2048;

        private const int MaxStackAlloc = 512;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Redaction));

        private static readonly Type[] AllowedCollectionTypes =
        [
            typeof(List<>),
            typeof(ArrayList),
            typeof(LinkedList<>),
            typeof(Stack),
            typeof(Stack<>),
            typeof(ConcurrentStack<>),
            typeof(Queue),
            typeof(Queue<>),
            typeof(ConcurrentQueue<>),
            typeof(HashSet<>),
            typeof(SortedSet<>),
            typeof(ConcurrentBag<>),
            typeof(BlockingCollection<>)
        ];

        private static readonly Type[] AllowedDictionaryTypes =
        [
            typeof(Dictionary<,>),
            typeof(SortedList),
            typeof(SortedList<,>),
            typeof(SortedDictionary<,>),
            typeof(ConcurrentDictionary<,>),
            typeof(Hashtable)
        ];

        private static readonly string[] AllowedSpecialCasedCollectionTypeNames = []; // "RangeIterator"

        private static readonly Type[] DeniedTypes =
        [
            typeof(SecureString)
        ];

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

        private static Redaction _instnace = new();

        private readonly Trie _typeTrie;

        private readonly HashSet<string> _redactedTypes;

        private readonly HashSet<string> _redactKeywords;

        // Replaced, not cleared, by SetConfig so a lookup racing with it cannot store a stale result in the new cache.
        private RedactionCache<Type> _typeCache;

        private RedactionCache<string> _keywordCache;

        private Redaction()
        {
            _typeTrie = new Trie();
            _redactedTypes = [];
            _redactKeywords =
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
            _typeCache = new(MaxCachedTypes);
            _keywordCache = new(MaxCachedKeywords);
        }

        internal static Redaction Instance => _instnace;

        [TestingOnly]
        internal int CachedTypesCount => Volatile.Read(ref _typeCache).Count;

        [TestingOnly]
        internal int CachedKeywordsCount => Volatile.Read(ref _keywordCache).Count;

        internal static bool IsSafeToCallToString(Type? type)
        {
            if (type is null)
            {
                return false;
            }

            var effectiveType = Nullable.GetUnderlyingType(type) ?? type;

            return TypeExtensions.IsSimple(effectiveType) ||
                   IsAllowedTypeSafeToCallToString(effectiveType);
        }

        internal static bool IsSupportedDictionary(object? o)
        {
            if (o == null)
            {
                return false;
            }

            return IsSupportedDictionary(o.GetType());
        }

        internal static bool IsSupportedDictionary(Type? type)
        {
            if (type == null)
            {
                return false;
            }

            return IsSupportedType(type, AllowedDictionaryTypes);
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

            if (IsSupportedType(type, AllowedCollectionTypes))
            {
                return true;
            }

            for (var i = 0; i < AllowedSpecialCasedCollectionTypeNames.Length; i++)
            {
                if (AllowedSpecialCasedCollectionTypeNames[i].Equals(type.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSupportedType(Type type, Type[] allowedTypes)
        {
            Type? genericDefinition = null;
            if (type.IsGenericType)
            {
                genericDefinition = type.GetGenericTypeDefinition();
            }

            for (var i = 0; i < allowedTypes.Length; i++)
            {
                var allowedType = allowedTypes[i];
                if (allowedType == type ||
                    (genericDefinition != null && allowedType == genericDefinition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAllowedTypeSafeToCallToString(Type type)
        {
            for (var i = 0; i < AllowedTypesSafeToCallToString.Length; i++)
            {
                if (AllowedTypesSafeToCallToString[i] == type)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool IsRedactedType(Type? type)
        {
            if (type is null)
            {
                return false;
            }

            var cache = Volatile.Read(ref _typeCache);
            if (cache.TryGetValue(type, out var redacted))
            {
                return redacted;
            }

            redacted = CheckForRedactedType(type);
#if NETCOREAPP3_1_OR_GREATER
            // Caching a collectible type would keep its AssemblyLoadContext from unloading.
            if (!type.IsCollectible)
            {
                cache.TryAdd(type, redacted);
            }
#else
            cache.TryAdd(type, redacted);
#endif

            return redacted;
        }

        private bool CheckForRedactedType(Type type)
        {
            return CheckForRedactedTypeCore(type) ||
                   (Nullable.GetUnderlyingType(type) is { } underlyingType &&
                    CheckForRedactedTypeCore(underlyingType));
        }

        private bool CheckForRedactedTypeCore(Type type)
        {
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

            if (_redactedTypes.Contains(typeFullName))
            {
                return true;
            }

            if (_typeTrie.HasMatchingPrefix(typeFullName))
            {
                var stringStartsWith = _typeTrie.GetStringStartingWith(typeFullName);
                return string.IsNullOrEmpty(stringStartsWith) || stringStartsWith.Length == typeFullName.Length;
            }

            return false;
        }

        internal bool IsRedactedKeyword(string name)
        {
            if (StringUtil.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.Length > MaxStackAlloc)
            {
                // Too long to normalize, so it never matches. Not caching it avoids retaining large customer strings.
                Log.Debug("Identifier length {Length} exceeds maximum allowed length of {MaxSize}, so it is skipped for redaction matching", name.Length, property1: MaxStackAlloc);
                return false;
            }

            var cache = Volatile.Read(ref _keywordCache);
            if (cache.TryGetValue(name, out var redacted))
            {
                return redacted;
            }

            redacted = CheckForRedactedKeyword(name);
            cache.TryAdd(name, redacted);
            return redacted;
        }

        internal bool CheckForRedactedKeyword(string keyword)
        {
            return TryNormalize(keyword, out var result) && _redactKeywords.Contains(result);
        }

        internal bool ShouldRedact(string name, Type type, out RedactionReason redactionReason)
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
                Log.Warning("Identifier length {Length} exceeds maximum allowed length of {MaxSize}, so it cannot be normalized and will be skipped for redaction matching", identifier.Length, property1: MaxStackAlloc);
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

        internal void SetConfig(HashSet<string> redactedIdentifiers, HashSet<string> redactedExcludedIdentifiers, HashSet<string> redactedTypes)
        {
#if NET6_0_OR_GREATER
            _redactKeywords.EnsureCapacity(_redactKeywords.Count + redactedIdentifiers.Count);
#endif
            foreach (var identifier in redactedIdentifiers)
            {
                if (TryNormalize(identifier, out var result))
                {
                    _redactKeywords.Add(result);
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
                    _redactKeywords.Remove(result);
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
                    _typeTrie.Insert(type[..^1]);
#else
                    var newTypeName = type.Substring(0, type.Length - 1);
                    _typeTrie.Insert(newTypeName);
#endif
                }
                else
                {
                    _redactedTypes.Add(type);
                }
            }

            // Results cached under the previous configuration may be stale.
            Volatile.Write(ref _typeCache, new RedactionCache<Type>(MaxCachedTypes));
            Volatile.Write(ref _keywordCache, new RedactionCache<string>(MaxCachedKeywords));
        }

        /// <summary>
        /// For unit tests only!
        /// </summary>
        internal void ResetInstance()
        {
           System.Threading.Interlocked.Exchange(ref _instnace, new());
        }
    }
}
