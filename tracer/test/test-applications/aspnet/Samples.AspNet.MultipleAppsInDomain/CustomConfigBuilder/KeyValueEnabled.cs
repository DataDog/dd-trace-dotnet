// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// Possible modes (or behaviors) for key/value substitution.
    /// </summary>
    public enum KeyValueEnabled
    {
        /// <summary>
        /// Will execute KeyValueConfigurationBuilder and throw on error.
        /// </summary>
        Enabled,
        /// <summary>
        /// Will not execute KeyValueConfigurationBuilder.
        /// </summary>
        Disabled,
        /// <summary>
        /// Will execute KeyValueConfigurationBuilder but not report errors.
        /// </summary>
        Optional,

        // For convenience, allow true/false in the builder attribute as well.
        /// <summary>
        /// Same as Enabled.
        /// </summary>
        True = Enabled,
        /// <summary>
        /// Same as Disabled.
        /// </summary>
        False = Disabled
    }
}