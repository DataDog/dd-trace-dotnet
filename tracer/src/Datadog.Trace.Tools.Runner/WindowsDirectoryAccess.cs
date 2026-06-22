// <copyright file="WindowsDirectoryAccess.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Datadog.Trace.Tools.Runner
{
#pragma warning disable CA1416 // Windows ACL APIs are only called after a RuntimeInformation Windows guard.
    internal static class WindowsDirectoryAccess
    {
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

        internal static void ValidateDirectoryAccess(string path, bool requireCurrentUserOwner, bool allowBroadWrite)
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

            if (requireCurrentUserOwner)
            {
                var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
                if (owner is null || !owner.Equals(currentUser))
                {
                    throw new IOException($"Directory '{path}' must be owned by the current user.");
                }
            }

            if (allowBroadWrite)
            {
                return;
            }

            foreach (FileSystemAccessRule rule in security.GetAccessRules(includeExplicit: true, includeInherited: true, targetType: typeof(SecurityIdentifier)))
            {
                if (rule.AccessControlType != AccessControlType.Allow ||
                    !GrantsWriteAccess(rule.FileSystemRights) ||
                    rule.IdentityReference is not SecurityIdentifier securityIdentifier ||
                    IsAllowedWriter(securityIdentifier, currentUser))
                {
                    continue;
                }

                throw new IOException($"Directory '{path}' must not grant write access to Windows identity '{securityIdentifier.Value}'.");
            }
        }

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

        private static bool IsAllowedWriter(SecurityIdentifier securityIdentifier, SecurityIdentifier currentUser)
        {
            return securityIdentifier.Equals(currentUser) ||
                   securityIdentifier.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
                   securityIdentifier.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
        }
    }
#pragma warning restore CA1416
}
