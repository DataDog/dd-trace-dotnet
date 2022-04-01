// <copyright file="UserDetails.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    /// <summary>
    /// A data container class for the users details
    /// </summary>
    public class UserDetails
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserDetails"/> class.
        /// </summary>
        public UserDetails()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserDetails"/> class.
        /// </summary>
        /// <param name="email">Sets the user's email address</param>
        /// <param name="name">Sets the user's name as displayed in the UI</param>
        /// <param name="id">Sets the unique identifier assoicated with the users</param>
        /// <param name="sessionId">Sets the user's session unique identifier</param>
        /// <param name="role">Sets the role associated with the user</param>
        public UserDetails(string email, string name, string id, string sessionId, string role)
        {
            Email = email;
            Name = name;
            Id = id;
            SessionId = sessionId;
            Role = role;
        }

        /// <summary>
        /// Gets or sets the user's email address
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the user's name as displayed in the UI
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier assoicated with the users
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the user's session unique identifier
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the role associated with the user
        /// </summary>
        public string Role { get; set; }
    }
}
