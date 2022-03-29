// <copyright file="StringInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Runtime.Serialization;

namespace Datadog.Demos.WcfService.Library
{
    [DataContract]
    public class StringInfo
    {
        private string _stringValue;
        private int _hashCode;

        public StringInfo()
        {
            _stringValue = null;
            _hashCode = 0;
        }

        public StringInfo(string stringValue, int hashCode)
        {
            _stringValue = stringValue;
            _hashCode = hashCode;
        }

        [DataMember]
        public string StringValue
        {
            get { return _stringValue; }
            set { _stringValue = value; }
        }

        [DataMember]
        public int HashCode
        {
            get { return _hashCode; }
            set { _hashCode = value; }
        }
    }
}
