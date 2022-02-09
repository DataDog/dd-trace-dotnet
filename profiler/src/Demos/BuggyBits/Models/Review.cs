// <copyright file="Review.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace BuggyBits.Models
{
    public class Review
    {
        public Review(int i)
        {
            var quotes = new string[]
            {
                "Buggy Bits is the best thing since sliced bread",
                "I have never seen such buggy bits, Buggy Bits are truly breaking new ground",
                "Once you have started using Buggy Bits there is no going back",
                "Truly amazing",
                "We have been using Buggy Bits since 1995 and the quality is always outstanding",
                "Buggy Bits always delivers what it promises"
            };
            var sources = new string[]
            {
                "Bug Bashers",
                "Delusional Software inc",
                "BuggySite.com",
                "The Bug Observer",
                "Bug Magazine",
                "Bug Chronicles"
            };
            Random random = new Random();
            int index = random.Next(3);
            Quote = quotes[i + index];
            Source = sources[i + index];
        }

        ~Review()
        {
            if (Quote.ToString() != string.Empty)
            {
                Quote = null;
            }
        }

        public string Quote { get; set; }
        public string Source { get; set; }

        public void Clear()
        {
            Quote = null;
            Source = null;
        }
    }
}
