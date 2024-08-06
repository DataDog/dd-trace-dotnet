// <copyright file="UserDetails.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Util;

namespace Datadog.Trace
{
    /// <summary>
    /// A data container class for the users details
    /// </summary>
    public struct UserDetails
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserDetails"/> struct.
        /// </summary>
        /// <param name="id">The unique identifier associated with the users</param>
        public UserDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                ThrowHelper.ThrowArgumentException(nameof(id) + " must be set to a value other than null or the empty string", nameof(id));
            }

            Id = id;
            PropagateId = false;
            Email = null;
            Name = null;
            SessionId = null;
            Role = null;
            Scope = null;
        }

        /// <summary>
        /// Gets or sets the user's email address
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets the user's name as displayed in the UI
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier associated with the user.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Id field should be propagated to other services called.
        /// NOTE: setting this value to true will automatically propagate the Id field to all downstream services
        /// including any third-party services called by the application. Therefore this value should only be set
        /// to true if the user is confident that it is safe to propagate the value to all downstream services
        /// called by the application.
        /// Default value is false.
        /// </summary>
        public bool PropagateId { get; set; }

        /// <summary>
        /// Gets or sets the user's session unique identifier
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Gets or sets the role associated with the user
        /// </summary>
        public string? Role { get; set; }

        /// <summary>
        /// Gets or sets the scopes or granted authorities the client currently possesses extracted from token or application security context
        /// </summary>
        public string? Scope { get; set; }
    }
}
