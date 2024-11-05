// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Configuration;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    internal class KeyValueExceptionHelper
    {
        public static Exception CreateKVCException(string msg, Exception ex, ConfigurationBuilder cb)
        {

            // If it's a ConfigurationErrorsException though, that means its coming from a re-entry to the
            // config system. That's where the root issue is, and that's the "Error Message" we want on
            // the top of the exception chain. So wrap it in another ConfigurationErrorsException of
            // sorts so the config system will use it instead of rolling it's own at this secondary
            // level.
            if (ex is ConfigurationErrorsException ceex)
            {
                var inner = new KeyValueConfigBuilderException($"'{cb.Name}' {msg} ==> {ceex.InnerException?.Message ?? ceex.Message}", ex.InnerException);
                return new KeyValueConfigurationErrorsException(ceex.Message, inner);
            }

            var ff = new KeyValueConfigBuilderException();
            return new KeyValueConfigBuilderException($"'{cb.Name}' {msg}: {ex.Message}", ex);
        }

        // We only want to wrap the original exception. Once we wrap it, just keep raising the wrapped
        // exception so we don't create an endless chain of exception wrappings that are not helpful when
        // being surfaced in a YSOD or similar. Use this helper to determine if wrapping is needed.
        public static bool IsKeyValueConfigException(Exception ex) => (ex is KeyValueConfigBuilderException) || (ex is KeyValueConfigurationErrorsException);
    }

    // There are two different exception types here because the .Net config system treats
    // ConfigurationErrorsExceptions differently. It considers it to be a pre-wrapped and ready for
    // presentation exception. Other exceptions get wrapped by the config system. We don't want
    // to lose that "pre-wrapped-ness" if the exception has already been through .Net config.

    /// <summary>
    /// An exception that wraps the root failure due to non-config exceptions while processing Key Value Config Builders.
    /// </summary>
    [Serializable]
    public class KeyValueConfigBuilderException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KeyValueConfigBuilderException"/> class.
        /// </summary>
        public KeyValueConfigBuilderException() : base() { }

        internal KeyValueConfigBuilderException(string msg, Exception inner) : base(msg, inner) { }
    }

    /// <summary>
    /// An exception that wraps the root failure due to config exceptions while processing Key Value Config Builders.
    /// </summary>
    [Serializable]
    public class KeyValueConfigurationErrorsException : ConfigurationErrorsException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KeyValueConfigurationErrorsException"/> class.
        /// </summary>
        public KeyValueConfigurationErrorsException() : base() { }

        internal KeyValueConfigurationErrorsException(string msg, Exception inner) : base(msg, inner) { }
    }
}