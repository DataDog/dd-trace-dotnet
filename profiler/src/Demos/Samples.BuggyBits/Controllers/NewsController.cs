// <copyright file="NewsController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using BuggyBits.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace BuggyBits.Controllers
{
    public class NewsController : Controller
    {
        private static int _id = 0;
        private int _instanceId;

// #pragma warning disable IDE0052 // Remove unread private members | this field is used to better show memory leaks
//        private readonly int[] bits = new int[25000];
// #pragma warning restore IDE0052
        private IMemoryCache cache;
        private DateTime _creationTime;

        public NewsController(IMemoryCache cache)
        {
            _creationTime = DateTime.Now;
            _instanceId = Interlocked.Increment(ref _id);
            this.cache = cache;
            GC.Collect();
        }

        ~NewsController()
        {
            Console.WriteLine($"{DateTime.Now.ToShortTimeString()} | {(DateTime.Now - _creationTime).TotalSeconds,4} - ~NewsController #{_instanceId}");
        }

        public IActionResult Index()
        {
            string key = Guid.NewGuid().ToString();
            var cachedResult = cache.GetOrCreate(key, cacheEntry =>
            {
                //// Adding a sliding expiration will help to evict cache entries sooner
                //// but the LOH will become fragmented
                // cacheEntry.SlidingExpiration = TimeSpan.FromMinutes(5);

                cacheEntry.RegisterPostEvictionCallback(CacheRemovedCallback);
                cacheEntry.Priority = CacheItemPriority.NeverRemove;

                return new string($"New site launched " + DateTime.Now);
            });

            var news = new List<News>
            {
                new News() { Title = cachedResult }
            };
            return View(news);
        }

        private void CacheRemovedCallback(object key, object value, EvictionReason reason, object state)
        {
            if (reason == EvictionReason.Capacity)
            {
                Console.WriteLine($"Cache entry {key} = '{value}' was removed due to capacity");
            }
        }
    }
}
