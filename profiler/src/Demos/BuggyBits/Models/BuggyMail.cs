// <copyright file="BuggyMail.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Text.RegularExpressions;

namespace BuggyBits.Models
{
    public class BuggyMail
    {
        public void SendEmail(string message, string emailAddres)
        {
            try
            {
                if (IsValidEmailAddress(emailAddres))
                {
                    // send an email with the message
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogException(ex);
            }
        }

        public bool IsValidEmailAddress(string emailAddress)
        {
            if (!Regex.IsMatch(emailAddress, "^([a-zA-Z0-9_]+)@([a-zA-Z0-9]+).([a-zA-Z]{2,5})$"))
            {
                throw new System.Exception("The email entered is not a valid email address");
            }
            else
            {
                return true;
            }
        }
    }
}
