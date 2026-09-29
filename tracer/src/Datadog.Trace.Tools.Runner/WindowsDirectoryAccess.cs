// <copyright file="WindowsDirectoryAccess.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Datadog.Trace.Tools.Runner;

#pragma warning disable CA1416 // Windows ACL APIs are only called after a RuntimeInformation Windows guard.
/// <summary>
/// Provides Windows directory creation and ACL validation for runner tracer home caches.
/// </summary>
internal static class WindowsDirectoryAccess
{
    /// <summary>
    /// Creates a directory with an explicit private ACL for the current user, LocalSystem, and Administrators.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    internal static void CreatePrivateDirectory(string path)
    {
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is null)
        {
            throw new IOException("Unable to determine the current Windows user.");
        }

        // Avoid inheriting broad write ACEs from user-configurable cache roots.
        var security = new DirectorySecurity();
        security.SetOwner(currentUser);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddDirectoryFullControl(security, currentUser);
        AddDirectoryFullControl(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));
        AddDirectoryFullControl(security, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));

        FileSystemAclExtensions.Create(new DirectoryInfo(path), security);
    }

    /// <summary>
    /// Validates Windows ownership and write ACLs for an existing directory.
    /// </summary>
    /// <param name="path">The directory path to validate.</param>
    /// <param name="requireCurrentUserOwner">Whether the directory must be owned by the current Windows user.</param>
    /// <param name="requireTrustedOwner">Whether the directory must be owned by a trusted Windows identity.</param>
    /// <param name="allowBroadWrite">Whether write access by identities outside the trusted set is allowed.</param>
    internal static void ValidateDirectoryAccess(
        string path,
        bool requireCurrentUserOwner,
        bool requireTrustedOwner,
        bool allowBroadWrite)
    {
        DirectorySecurity security;
        try
        {
            security = FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(path), AccessControlSections.Access | AccessControlSections.Owner);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SystemException)
        {
            throw new IOException($"Unable to inspect Windows access control for directory '{path}'.", ex);
        }

        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is null)
        {
            throw new IOException("Unable to determine the current Windows user.");
        }

        if (requireCurrentUserOwner || requireTrustedOwner)
        {
            var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            var isRequiredOwner = owner is not null &&
                                  (requireCurrentUserOwner ? owner.Equals(currentUser) : IsAllowedWriter(owner, currentUser));
            if (!isRequiredOwner)
            {
                var ownerDescription = requireCurrentUserOwner ? "the current user" : "a trusted Windows identity";
                throw new IOException($"Directory '{path}' must be owned by {ownerDescription}.");
            }
        }

        if (allowBroadWrite)
        {
            return;
        }

        foreach (FileSystemAccessRule rule in security.GetAccessRules(includeExplicit: true, includeInherited: true, targetType: typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow ||
                (rule.PropagationFlags & PropagationFlags.InheritOnly) != 0 ||
                !GrantsWriteAccess(rule.FileSystemRights) ||
                rule.IdentityReference is not SecurityIdentifier securityIdentifier ||
                IsAllowedWriter(securityIdentifier, currentUser))
            {
                continue;
            }

            throw new IOException($"Directory '{path}' must not grant write access to Windows identity '{securityIdentifier.Value}'.");
        }
    }

    /// <summary>
    /// Adds an inheritable full-control access rule for a trusted Windows identity.
    /// </summary>
    /// <param name="security">The directory security descriptor to update.</param>
    /// <param name="securityIdentifier">The trusted Windows identity.</param>
    private static void AddDirectoryFullControl(DirectorySecurity security, SecurityIdentifier securityIdentifier)
    {
        security.AddAccessRule(
            new FileSystemAccessRule(
                securityIdentifier,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
    }

    /// <summary>
    /// Checks whether a rights mask can modify, delete, or take ownership of the directory.
    /// </summary>
    /// <param name="rights">The file-system rights to inspect.</param>
    /// <returns><c>true</c> when the rights include write-equivalent access.</returns>
    private static bool GrantsWriteAccess(FileSystemRights rights)
    {
        const FileSystemRights writeRights =
            FileSystemRights.Write |
            FileSystemRights.WriteData |
            FileSystemRights.AppendData |
            FileSystemRights.CreateFiles |
            FileSystemRights.CreateDirectories |
            FileSystemRights.WriteAttributes |
            FileSystemRights.WriteExtendedAttributes |
            FileSystemRights.Delete |
            FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.ChangePermissions |
            FileSystemRights.TakeOwnership |
            FileSystemRights.Modify |
            FileSystemRights.FullControl;

        return (rights & writeRights) != 0;
    }

    /// <summary>
    /// Checks whether a Windows identity is expected to have write access to the private cache.
    /// </summary>
    /// <param name="securityIdentifier">The Windows identity to inspect.</param>
    /// <param name="currentUser">The current Windows user identity.</param>
    /// <returns><c>true</c> when the identity is the current user, LocalSystem, or Builtin Administrators.</returns>
    private static bool IsAllowedWriter(SecurityIdentifier securityIdentifier, SecurityIdentifier currentUser)
    {
        return securityIdentifier.Equals(currentUser) ||
               securityIdentifier.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
               securityIdentifier.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
    }
}
#pragma warning restore CA1416
