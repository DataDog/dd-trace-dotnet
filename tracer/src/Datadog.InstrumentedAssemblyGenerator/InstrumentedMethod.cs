using System;
using System.IO;
using System.Linq;
using static Datadog.InstrumentedAssemblyGenerator.InstrumentedAssemblyGeneratorConsts;
#pragma warning disable CS1570

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal class InstrumentedMethod
    {
        internal InstrumentedMethod(
            Guid mvid,
            ulong typeToken,
            ulong methodToken,
            string moduleName,
            string typeName,
            string methodName,
            SigMemberType returnTypeName,
            SigMemberType[] argumentsNames,
            SigMemberType[] locals,
            bool isStatic,
            string methodIlFilePath)
        {
            Mvid = mvid;
            TypeToken = typeToken;
            MethodToken = methodToken;
            ModuleName = moduleName;
            TypeName = typeName;
            MethodName = methodName;
            ReturnType = returnTypeName;
            ArgumentsNames = argumentsNames;
            Locals = locals;
            IsStatic = isStatic;
            _methodIlFilePath = methodIlFilePath;
            Code = new Lazy<byte[]>(GetIlBytesFromFile);
        }

        private byte[] GetIlBytesFromFile()
        {
            return File.ReadAllBytes(_methodIlFilePath);
        }

        private readonly string _methodIlFilePath;
        internal Guid? Mvid { get; }
        internal string ModuleName { get; }
        internal ulong TypeToken { get; }
        internal string TypeName { get; }
        internal SigMemberType ReturnType { get; }
        internal SigMemberType[] ArgumentsNames { get; }
        internal SigMemberType[] Locals { get; }
        internal ulong MethodToken { get; }
        internal string MethodName { get; }
        internal bool IsStatic { get; }
        internal Lazy<byte[]> Code { get; }


        /// <summary>
        /// File content pattern:
        /// 0: 279430E-35CD-4177-B100-751FC0E07B17}@ // module id
        /// 1: 2000020@ // type token
        /// 2: 60001b0@ // method token
        /// 3: System.Net.Requests.dll@ // assembly name
        /// 4: System.Net.WebRequest@ // type name
        /// 5: GetResponseAsync@ // method name
        /// 6: 0x00000015?0x00000012?0x0100000c?System.Threading.Tasks.Task`1<0x00000012?0x02000028?System.Net.WebResponse>@ // return type name
        /// 7: 0x0000001d?0x0000000e@ // arguments
        /// 8: 0x00000015?0x00000012?0x0100000c?System.Threading.Tasks.Task`1<0x00000012?0x02000028?System.Net.WebResponse>@ // locals
        /// 9: 1 // has this
        /// </summary>
        /// <param name="filePath">File path to text file that contains the instrumented method info</param>
        /// <returns>InstrumentedMethod object</returns>
        internal static InstrumentedMethod ReadFromFile(string filePath)
        {
            string[] parts = File.ReadLines(filePath).First().Split(MetadataValueSeparator.ToCharArray());

            if (parts.Length != InstrumentedLogFileParts)
            {
                throw new ArgumentException(nameof(filePath));
            }

            //RefEmit_InMemoryManifestModule and others
            //TODO: linux
            if (!parts[3].EndsWith(".dll") && !parts[3].EndsWith(".exe"))
            {
                return null;
            }

            string[] locals = MetadataNameParser.SanitizeGenericSig(parts[8]);

            return new InstrumentedMethod(
                Guid.Parse(parts[0]),
                ulong.Parse(parts[1], System.Globalization.NumberStyles.HexNumber),
                ulong.Parse(parts[2], System.Globalization.NumberStyles.HexNumber),
                parts[3],
                parts[4],
                parts[5],
                new SigMemberType(parts[6]),
                MetadataNameParser.SplitArgumentsOrLocals(parts[7]).Select(p => new SigMemberType(p)).ToArray(),
                locals.Select(p => new SigMemberType(p)).ToArray(),
                int.Parse(parts[9]) == 0,
                filePath.Replace(TextFilePrefix, BinaryFilePrefix));
        }

        public override string ToString()
        {
            return $"{TypeName}.{MethodName}";
        }
    }
}
