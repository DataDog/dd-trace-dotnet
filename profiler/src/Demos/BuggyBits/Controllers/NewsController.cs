// <copyright file="NewsController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using BuggyBits.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace BuggyBits.Controllers
{
    public class NewsController : Controller
    {
#pragma warning disable IDE0052 // Remove unread private members | this field is used to better show memory leaks
        private readonly int[] bits = new int[100000];
#pragma warning restore IDE0052
        private IMemoryCache cache;

        public NewsController(IMemoryCache cache)
        {
            this.cache = cache;
        }

        public IActionResult Index()
        {
            string key = Guid.NewGuid().ToString();
            var cachedResult = cache.GetOrCreate(key, cacheEntry =>
            {
                cacheEntry.SlidingExpiration = TimeSpan.FromMinutes(5);
                cacheEntry.RegisterPostEvictionCallback(CacheRemovedCallback);
                cacheEntry.Priority = CacheItemPriority.NeverRemove;

                return new string("New site launched 2008-02-02");
            });

            var news = new List<News>
            {
                new News() { Title = cachedResult }
            };
            return View(news);
        }

        private void CacheRemovedCallback(object key, object value, EvictionReason reason, object state)
        {
            throw new NotImplementedException();
        }
    }
}
