// <copyright file="Utility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;

namespace BuggyBits.Models
{
    public static class Utility
    {
        public static void WriteToLog(string message, string fileName)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(fileName))
                {
                    // Log the event with date and time.
                    sw.WriteLine("--------------------------");
                    sw.WriteLine(DateTime.Now.ToLongTimeString());
                    sw.WriteLine("-------------------");
                    sw.WriteLine(message);
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogException(ex);
            }
        }
    }
}
