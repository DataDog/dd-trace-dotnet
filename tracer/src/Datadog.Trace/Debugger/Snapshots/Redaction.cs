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
        // Identifiers come from probed code, so 2048 covers typical apps. Cached names are at most
        // MaxCachedNameLength chars, which bounds this cache to about 2 MB in the worst case.
        [TestingAndPrivateOnly]
        internal const int MaxCachedKeywords = 2048;

        // Data keys come from customer data and churn, so this cache is smaller and expected to reset.
        [TestingAndPrivateOnly]
        internal const int MaxCachedDataKeys = 1024;

        // Captured types are usually a few hundred per process; 2048 leaves headroom for about 100 KB.
        private const int MaxCachedTypes = 2048;

        // Longer names are still checked but never cached, so large customer strings are not retained.
        private const int MaxCachedNameLength = 512;

        // 256 chars is 512 bytes, the stackalloc limit. Longer normalized names use a pooled buffer.
        private const int MaxStackAllocChars = 256;

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

        private static int _longNameLogged;

        private readonly Trie _typeTrie;

        private readonly HashSet<string> _redactedTypes;

        private readonly HashSet<string> _redactKeywords;

        // Upper bound on the length of any keyword, so longer normalized names are rejected without allocating.
        private int _maxKeywordLength;

        // Replaced, not cleared, by SetConfig so a lookup racing with it cannot store a stale result in the new cache.
        private RedactionCache<Type> _typeCache;

        private RedactionCache<string> _keywordCache;

        private RedactionCache<string> _dataKeyCache;

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
            _maxKeywordLength = GetMaxLength(_redactKeywords);
            _typeCache = new(MaxCachedTypes);
            _keywordCache = new(MaxCachedKeywords);
            _dataKeyCache = new(MaxCachedDataKeys);
        }

        internal static Redaction Instance => _instnace;

        [TestingOnly]
        internal int CachedTypesCount => Volatile.Read(ref _typeCache).Count;

        [TestingOnly]
        internal int CachedKeywordsCount => Volatile.Read(ref _keywordCache).Count;

        [TestingOnly]
        internal int CachedDataKeysCount => Volatile.Read(ref _dataKeyCache).Count;

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

        private static void AddToCache<TKey>(ref RedactionCache<TKey> field, RedactionCache<TKey> cache, TKey key, bool value)
            where TKey : notnull
        {
            if (cache.TryAdd(key, value))
            {
                return;
            }

            // Full: start over so new hot keys can be cached again. The compare-exchange keeps a cache installed by SetConfig.
            var fresh = new RedactionCache<TKey>(cache.Capacity);
            fresh.TryAdd(key, value);
            Interlocked.CompareExchange(ref field, fresh, cache);
        }

        private static int GetMaxLength(HashSet<string> values)
        {
            var max = 0;
            foreach (var value in values)
            {
                if (value.Length > max)
                {
                    max = value.Length;
                }
            }

            return max;
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
                AddToCache(ref _typeCache, cache, type, redacted);
            }
#else
            AddToCache(ref _typeCache, cache, type, redacted);
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

        /// <summary>
        /// Checks an identifier from probed code, such as a variable, field or property name.
        /// </summary>
        internal bool IsRedactedKeyword(string name) => IsRedactedName(name, ref _keywordCache);

        /// <summary>
        /// Checks a key taken from customer data, such as a dictionary key. Data keys have their own cache
        /// so that unique keys cannot keep resetting the identifier cache.
        /// </summary>
        internal bool IsRedactedDataKey(string? key) => IsRedactedName(key, ref _dataKeyCache);

        internal bool CheckForRedactedKeyword(string keyword)
        {
            return TryNormalize(keyword, _maxKeywordLength, out var result) && _redactKeywords.Contains(result);
        }

        internal bool ShouldRedact(string name, Type type, out RedactionReason redactionReason)
            => ShouldRedactCore(IsRedactedKeyword(name), type, out redactionReason);

        internal bool ShouldRedactDataKey(string? key, Type type, out RedactionReason redactionReason)
            => ShouldRedactCore(IsRedactedDataKey(key), type, out redactionReason);

        private static string Normalize(string identifier)
        {
            TryNormalize(identifier, int.MaxValue, out var result);
            return result;
        }

        /// <summary>
        /// Lowercases <paramref name="identifier"/> and removes separator characters. Returns false, without
        /// allocating, when the result would be longer than <paramref name="maxLength"/>.
        /// </summary>
        private static unsafe bool TryNormalize(string identifier, int maxLength, out string result)
        {
            result = identifier;
            var normalizedLength = 0;
            var needsNormalization = false;
            for (var i = 0; i < identifier.Length; i++)
            {
                var c = identifier[i];
                if (IsRemovableChar(c))
                {
                    needsNormalization = true;
                }
                else if (++normalizedLength > maxLength)
                {
                    return false;
                }
                else if (char.IsUpper(c))
                {
                    needsNormalization = true;
                }
            }

            if (!needsNormalization)
            {
                return true;
            }

            if (normalizedLength <= MaxStackAllocChars)
            {
                var buffer = stackalloc char[normalizedLength];
                result = WriteNormalized(identifier, buffer);
            }
            else
            {
                var rented = ArrayPool<char>.Shared.Rent(normalizedLength);
                try
                {
                    fixed (char* buffer = rented)
                    {
                        result = WriteNormalized(identifier, buffer);
                    }
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }

            return true;
        }

        private static unsafe string WriteNormalized(string identifier, char* buffer)
        {
            var written = 0;
            for (var i = 0; i < identifier.Length; i++)
            {
                var c = identifier[i];
                if (char.IsUpper(c))
                {
                    buffer[written++] = char.ToLowerInvariant(c);
                }
                else if (!IsRemovableChar(c))
                {
                    buffer[written++] = c;
                }
            }

            return new string(buffer, 0, written);
        }

        private static bool IsRemovableChar(char c)
        {
            return c is '_' or '-' or '$' or '@';
        }

        private bool IsRedactedName(string? name, ref RedactionCache<string> cacheField)
        {
            if (StringUtil.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.Length > MaxCachedNameLength)
            {
                if (Volatile.Read(ref _longNameLogged) == 0 && Interlocked.Exchange(ref _longNameLogged, 1) == 0)
                {
                    Log.Warning("Name length {Length} exceeds {MaxLength} characters, so its redaction result is not cached. Further occurrences are not logged.", name.Length, property1: MaxCachedNameLength);
                }

                return CheckForRedactedKeyword(name);
            }

            var cache = Volatile.Read(ref cacheField);
            if (cache.TryGetValue(name, out var redacted))
            {
                return redacted;
            }

            redacted = CheckForRedactedKeyword(name);
            AddToCache(ref cacheField, cache, name, redacted);
            return redacted;
        }

        private bool ShouldRedactCore(bool isRedactedName, Type type, out RedactionReason redactionReason)
        {
            if (isRedactedName)
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

        internal void SetConfig(HashSet<string> redactedIdentifiers, HashSet<string> redactedExcludedIdentifiers, HashSet<string> redactedTypes)
        {
#if NET6_0_OR_GREATER
            _redactKeywords.EnsureCapacity(_redactKeywords.Count + redactedIdentifiers.Count);
#endif
            foreach (var identifier in redactedIdentifiers)
            {
                var normalized = Normalize(identifier);

                // Raise the bound before adding, so a concurrent lookup never rejects the new keyword by length.
                if (normalized.Length > _maxKeywordLength)
                {
                    _maxKeywordLength = normalized.Length;
                }

                _redactKeywords.Add(normalized);
            }

            foreach (var excluded in redactedExcludedIdentifiers)
            {
                _redactKeywords.Remove(Normalize(excluded));
            }

            _maxKeywordLength = GetMaxLength(_redactKeywords);

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
            Volatile.Write(ref _dataKeyCache, new RedactionCache<string>(MaxCachedDataKeys));
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
