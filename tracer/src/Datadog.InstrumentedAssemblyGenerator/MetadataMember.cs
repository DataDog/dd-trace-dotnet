using System;

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal class MetadataMember
    {
        internal static MetadataMember Empty = new() { IsEmpty = true };
        internal string Type { get; }
        internal string MethodOrField { get; }
        internal bool IsEmpty { get; private set; }
        internal bool IsGenericType { get; }
        internal bool IsGenericMethod { get; }
        //TODO: Consider implementing FullName like 'FullNameFactory.MethodFullName'
        internal string FullName { get; }
        internal SigMemberType[] Parameters { get; }
        internal SigMemberType TypeSig { get; }
        internal SigMemberType ReturnTypeSig { get; }
        internal SigMemberType[] TypeGenericParameters { get; set; }
        internal SigMemberType[] MethodGenericParameters { get; set; }
        internal int MethodOrFieldAttr { get; set; }

        internal static MetadataMember Create(Token token, string name)
        {
            return MetadataNameParser.Parse(token, name);
        }

        internal MetadataMember() { }

        internal MetadataMember(string type, string fullName) :
            this(type, "", fullName, Array.Empty<SigMemberType>())
        { }

        internal MetadataMember(string type, string methodOrField, string fullName, SigMemberType[] parameters) :
            this(type, methodOrField, fullName, parameters, null, Array.Empty<SigMemberType>(), Array.Empty<SigMemberType>(), null, 0)
        { }

        internal MetadataMember(string type, string fullName, SigMemberType[] parameters, SigMemberType[] typeGenericParameters) :
            this(type, "", fullName, parameters, null, typeGenericParameters, Array.Empty<SigMemberType>(), null, 0)
        { }

        internal MetadataMember(string type, string methodOrField, string fullName, SigMemberType[] parameters, SigMemberType typeSig, SigMemberType[] typeGenericParameters, SigMemberType[] methodGenericParameters, SigMemberType returnTypeSig, int methodOrFieldAttr)
        {
            Type = type;
            MethodOrField = methodOrField;
            FullName = fullName;
            Parameters = parameters;
            IsGenericType = typeGenericParameters.Length > 0;
            IsGenericMethod = methodGenericParameters.Length > 0;
            TypeSig = typeSig;
            TypeGenericParameters = typeGenericParameters;
            MethodGenericParameters = methodGenericParameters;
            ReturnTypeSig = returnTypeSig;
            MethodOrFieldAttr  = methodOrFieldAttr;
            IsEmpty = false;
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Type))
            {
                return MethodOrField;
            }

            if (string.IsNullOrWhiteSpace(MethodOrField))
            {
                return Type;
            }

            return Type + "." + MethodOrField;
        }
    }
}