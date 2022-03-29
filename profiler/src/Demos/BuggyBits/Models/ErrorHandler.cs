// <copyright file="ErrorHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace BuggyBits.Models
{
    public class ErrorHandler
    {
        public static void LogException(Exception ex)
        {
            Utility.WriteToLog(ex.Message, "c:\\log.txt");
        }
    }
}
