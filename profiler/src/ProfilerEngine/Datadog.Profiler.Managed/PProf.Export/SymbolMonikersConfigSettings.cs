// <copyright file="SymbolMonikersConfigSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Datadog.Util;

namespace Datadog.PProf.Export
{
    public class SymbolMonikersConfigSettings
    {
        private readonly PProfBuilder _ownerBuilder;
        private bool _isReadOnly = false;
        private string _unknownFunctionName = "Unknown-Function-or-Method";
        private string _unknownClassTypeName = "Unknown-Type-or-Class";
        private string _unknownBinaryContainerName = "Unknown-Assembly-or-Library";
        private string _unknownBinaryContainerVersion = "Unknown-Version";
        private string _unknownEntrypointBinaryName = "Unknown-Entrypoint-Executable-Assembly-or-Library";
        private string _prefixBinaryContainerName = " |lm:";
        private string _prefixNamespaceName = "|ns:";
        private string _prefixClassTypeName = "|ct:";
        private string _prefixFunctionName = " |fn:";
        private string _binaryContainerWithVersionTemplate = "{0}(ver: {1})";
        private bool _includeBinaryContainerNameIntoApiName = true;
        private bool _displayVersionIfUnknown = false;

        internal SymbolMonikersConfigSettings(PProfBuilder ownerBuilder)
        {
            Validate.NotNull(ownerBuilder, nameof(ownerBuilder));
            _ownerBuilder = ownerBuilder;
        }

        public bool IsReadOnly
        {
            get { return _isReadOnly; }
        }

        public string UnknownFunctionName
        {
            get
            {
                return _unknownFunctionName;
            }

            set
            {
                ValidateForModification();
                _unknownFunctionName = value;
            }
        }

        public string UnknownClassTypeName
        {
            get
            {
                return _unknownClassTypeName;
            }

            set
            {
                ValidateForModification();
                _unknownClassTypeName = value;
            }
        }

        public string UnknownBinaryContainerName
        {
            get
            {
                return _unknownBinaryContainerName;
            }

            set
            {
                ValidateForModification();
                _unknownBinaryContainerName = value;
            }
        }

        public string UnknownBinaryContainerVersion
        {
            get
            {
                return _unknownBinaryContainerVersion;
            }

            set
            {
                ValidateForModification();
                _unknownBinaryContainerVersion = value;
            }
        }

        public string UnknownEntrypointBinaryName
        {
            get
            {
                return _unknownEntrypointBinaryName;
            }

            set
            {
                ValidateForModification();
                _unknownEntrypointBinaryName = value;
            }
        }

        public string PrefixBinaryContainerName
        {
            get
            {
                return _prefixBinaryContainerName;
            }

            set
            {
                ValidateForModification();
                _prefixBinaryContainerName = value;
            }
        }

        public string PrefixNamespaceName
        {
            get
            {
                return _prefixNamespaceName;
            }

            set
            {
                ValidateForModification();
                _prefixNamespaceName = value;
            }
        }

        public string PrefixClassTypeName
        {
            get
            {
                return _prefixClassTypeName;
            }

            set
            {
                ValidateForModification();
                _prefixClassTypeName = value;
            }
        }

        public string PrefixFunctionName
        {
            get
            {
                return _prefixFunctionName;
            }

            set
            {
                ValidateForModification();
                _prefixFunctionName = value;
            }
        }

        public string BinaryContainerWithVersionTemplate
        {
            get
            {
                return _binaryContainerWithVersionTemplate;
            }

            set
            {
                Validate.NotNullOrWhitespace(value, nameof(BinaryContainerWithVersionTemplate));
                ValidateForModification();
                _binaryContainerWithVersionTemplate = value;
            }
        }

        public bool IncludeBinaryContainerNameIntoApiName
        {
            get
            {
                return _includeBinaryContainerNameIntoApiName;
            }

            set
            {
                ValidateForModification();
                _includeBinaryContainerNameIntoApiName = value;
            }
        }

        public bool DisplayVersionIfUnknown
        {
            get
            {
                return _displayVersionIfUnknown;
            }

            set
            {
                ValidateForModification();
                _displayVersionIfUnknown = value;
            }
        }

        internal void SetReadOnly()
        {
            _isReadOnly = true;
        }

        private void ValidateForModification()
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException(
                            $"Cannot modify this {nameof(SymbolMonikersConfigSettings)} instance,"
                            + " because it is set to read-only. It is not possible to modify"
                            + $" a {nameof(PProfBuilder)}'s {nameof(PProfBuilder.SymbolMonikersConfig)}"
                            + $" after its first {nameof(PProfBuildSession)} was started."
                            + $" Consider creating a new {nameof(PProfBuilder)}.");
            }
        }
    }
}