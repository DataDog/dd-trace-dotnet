using System;
using System.Security;
using System.Security.Permissions;

namespace AppDomain.Crash
{
    // Enumerated type for permission states.

    // Derive from CodeAccessPermission to gain implementations of the following
    // sealed IStackWalk methods: Assert, Demand, Deny, and PermitOnly.
    // Implement the following abstract IPermission methods: Copy, Intersect, and IsSubSetOf.
    // Implementing the Union method of the IPermission class is optional.
    // Implement the following abstract ISecurityEncodable methods: FromXml and ToXml.
    // Making the class 'sealed' is optional.

    public sealed class MockPermission : CodeAccessPermission, IPermission,
        IUnrestrictedPermission, ISecurityEncodable, ICloneable
    {
        private Boolean m_specifiedAsUnrestricted = false;
        private MockPermissionState m_flags = MockPermissionState.NoPermissions;

        // This constructor creates and initializes a permission with generic access.
        public MockPermission(PermissionState state)
        {
            m_specifiedAsUnrestricted = (state == PermissionState.Unrestricted);
        }

        // This constructor creates and initializes a permission with specific access.        
        public MockPermission(MockPermissionState flags)
        {
            if (!Enum.IsDefined(typeof(MockPermissionState), flags))
                throw new ArgumentException
                    ("flags value is not valid for the MockPermissionState enumerated type");
            m_specifiedAsUnrestricted = false;
            m_flags = flags;
        }

        // For debugging, return the state of this object as XML.
        public override String ToString() { return ToXml().ToString(); }

        // Private method to cast (if possible) an IPermission to the type.
        private MockPermission VerifyTypeMatch(IPermission target)
        {
            if (GetType() != target.GetType())
                throw new ArgumentException($"target must be of the {GetType().FullName} type");
            return (MockPermission)target;
        }

        // This is the Private Clone helper method. 
        private MockPermission Clone(Boolean specifiedAsUnrestricted, MockPermissionState flags)
        {
            MockPermission mockPerm = (MockPermission)Clone();
            mockPerm.m_specifiedAsUnrestricted = specifiedAsUnrestricted;
            mockPerm.m_flags = specifiedAsUnrestricted ? MockPermissionState.DomainNeutralAssembliesAllowed : m_flags;
            return mockPerm;
        }

        #region IPermission Members
        // Return a new object that contains the intersection of 'this' and 'target'.
        public override IPermission Intersect(IPermission target)
        {
            // If 'target' is null, return null.
            if (target == null) return null;

            // Both objects must be the same type.
            MockPermission mockPerm = VerifyTypeMatch(target);

            // If 'this' and 'target' are unrestricted, return a new unrestricted permission.
            if (m_specifiedAsUnrestricted && mockPerm.m_specifiedAsUnrestricted)
                return Clone(true, MockPermissionState.DomainNeutralAssembliesAllowed);

            // Calculate the intersected permissions. If there are none, return null.
            MockPermissionState val = (MockPermissionState)
                Math.Min((Int32)m_flags, (Int32)mockPerm.m_flags);
            if (val == 0) return null;

            // Return a new object with the intersected permission value.
            return Clone(false, val);
        }

        // Called by the Demand method: returns true if 'this' is a subset of 'target'.
        public override Boolean IsSubsetOf(IPermission target)
        {
            // If 'target' is null and this permission allows nothing, return true.
            if (target == null) return m_flags == 0;

            // Both objects must be the same type.
            MockPermission mockPerm = VerifyTypeMatch(target);

            // Return true if the permissions of 'this' is a subset of 'target'.
            return m_flags <= mockPerm.m_flags;
        }

        // Return a new object that matches 'this' object's permissions.
        public sealed override IPermission Copy()
        {
            return (IPermission)Clone();
        }

        // Return a new object that contains the union of 'this' and 'target'.
        // Note: You do not have to implement this method. If you do not, the version
        // in CodeAccessPermission does this:
        //    1. If target is not null, a NotSupportedException is thrown.
        //    2. If target is null, then Copy is called and the new object is returned.
        public override IPermission Union(IPermission target)
        {
            // If 'target' is null, then return a copy of 'this'.
            if (target == null) return Copy();

            // Both objects must be the same type.
            MockPermission mockPerm = VerifyTypeMatch(target);

            // If 'this' or 'target' are unrestricted, return a new unrestricted permission.
            if (m_specifiedAsUnrestricted || mockPerm.m_specifiedAsUnrestricted)
                return Clone(true, MockPermissionState.DomainNeutralAssembliesAllowed);

            // Return a new object with the calculated, unioned permission value.
            return Clone(false, (MockPermissionState)
                Math.Max((Int32)m_flags, (Int32)mockPerm.m_flags));
        }
        #endregion

        #region ISecurityEncodable Members
        // Populate the permission's fields from XML.
        public override void FromXml(SecurityElement e)
        {
            m_specifiedAsUnrestricted = false;
            m_flags = 0;

            // If XML indicates an unrestricted permission, make this permission unrestricted.
            String s = (String)e.Attributes["Unrestricted"];
            if (s != null)
            {
                m_specifiedAsUnrestricted = Convert.ToBoolean(s);
                if (m_specifiedAsUnrestricted)
                    m_flags = MockPermissionState.DomainNeutralAssembliesAllowed;
            }

            // If XML indicates a restricted permission, parse the flags.
            if (!m_specifiedAsUnrestricted)
            {
                s = (String)e.Attributes["Flags"];
                if (s != null)
                {
                    m_flags = (MockPermissionState)
                    Convert.ToInt32(Enum.Parse(typeof(MockPermission), s, true));
                }
            }
        }

        // Produce XML from the permission's fields.
        public override SecurityElement ToXml()
        {
            // These first three lines create an element with the required format.
            SecurityElement e = new SecurityElement("IPermission");
            // Replace the double quotation marks (“”) with single quotation marks (‘’)
            // to remain XML compliant when the culture is not neutral.
            e.AddAttribute("class", GetType().AssemblyQualifiedName.Replace('\"', '\''));
            e.AddAttribute("version", "1");

            if (!m_specifiedAsUnrestricted)
                e.AddAttribute("Flags", Enum.Format(typeof(MockPermissionState), m_flags, "G"));
            else
                e.AddAttribute("Unrestricted", "true");
            return e;
        }
        #endregion

        #region IUnrestrictedPermission Members
        // Returns true if permission is effectively unrestricted.
        public Boolean IsUnrestricted()
        {
            // This means that the object is unrestricted at runtime.
            return m_flags == MockPermissionState.DomainNeutralAssembliesAllowed;
        }
        #endregion

        #region ICloneable Members

        // Return a copy of the permission.
        public Object Clone() { return MemberwiseClone(); }

        #endregion
    }
}
