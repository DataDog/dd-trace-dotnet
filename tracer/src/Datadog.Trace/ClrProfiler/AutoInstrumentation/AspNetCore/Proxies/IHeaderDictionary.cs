// <copyright file="IHeaderDictionary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.Proxies
{
    internal interface IHeaderDictionary
    {
        // I need two indexers: One that returns a string, one that returns a string[]

        // TODO: May need to add a DuckAttribute to get this to work. After all, we're relying on the call
        // to the new one: new StringValues this[string key] { get; set; }
        // And we even though StringValues is not a string, it can be cast to one so our method will have a string return type
        // string this[string key] { get; }

        [Duck(Name = "Microsoft.AspNetCore.Http.IHeaderDictionary.get_Item")]
        string GetItemAsString(string key);

        [Duck(Name = "Microsoft.AspNetCore.Http.IHeaderDictionary.get_Item")]
        string[] GetItemAsStringArray(string key);

        [Duck(Name = "System.Collections.Generic.IDictionary<System.String,Microsoft.Extensions.Primitives.StringValues>.get_Keys")]
        ICollection<string> GetKeys();

        [Duck(ExplicitInterfaceTypeName = "System.Collections.Generic.IDictionary<System.String,Microsoft.Extensions.Primitives.StringValues>")]
        bool TryGetValue(string key, out IEnumerable<string> value);

        [Duck(ExplicitInterfaceTypeName = "System.Collections.Generic.IDictionary<System.String,Microsoft.Extensions.Primitives.StringValues>")]
        bool Remove(string key);
    }
}
#endif
