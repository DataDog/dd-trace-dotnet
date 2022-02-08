// <copyright file="StringProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Util;

namespace Datadog.Demos.WcfService.Library
{
    public class StringProvider : IStringProvider
    {
        private readonly Random _rnd = new Random();

        public string GenerateRandomAsciiString(int length)
        {
            StringBuilder str = new StringBuilder(capacity: length);

            lock (_rnd)
            {
                for (int i = 0; i < length; i++)
                {
                    str.Append((char)_rnd.Next(33, 126));
                }
            }

            return str.ToString();
        }

        public int ComputeStableHash(string str)
        {
            Validate.Equals(str, nameof(str));

            uint hash = Hash.ComputeFastStable(str);
            return (int)hash;
        }

        public StringInfo GenerateRandomAsciiStringWithHash(int length)
        {
            string str = GenerateRandomAsciiString(length);
            int hash = ComputeStableHash(str);
            return new StringInfo(str, hash);
        }
    }
}
