﻿using System;
using System.Resources;

namespace Datadog.FxResources.System.Reflection.Metadata
{
    public static class SR { }
}
namespace Datadog.System
{
    public static partial class SR
    {
        private static readonly bool s_usingResourceKeys = AppContext.TryGetSwitch("System.Resources.UseSystemResourceKeys", out bool usingResourceKeys) ? usingResourceKeys : false;

        // This method is used to decide if we need to append the exception message parameters to the message when calling SR.Format.
        // by default it returns the value of System.Resources.UseSystemResourceKeys AppContext switch or false if not specified.
        // Native code generators can replace the value this returns based on user input at the time of native code generation.
        // The trimming tools are also capable of replacing the value of this method when the application is being trimmed.
        public static bool UsingResourceKeys() => s_usingResourceKeys;

        public static string GetResourceString(string resourceKey)
        {
            if (UsingResourceKeys())
            {
                return resourceKey;
            }

            string? resourceString = null;
            try
            {
                resourceString =
#if SYSTEM_PRIVATE_CORELIB || NATIVEAOT
                    InternalGetResourceString(resourceKey);
#else
                    ResourceManager.GetString(resourceKey);
#endif
            }
            catch (MissingManifestResourceException) { }

            return resourceString!; // only null if missing resources
        }

        public static string GetResourceString(string resourceKey, string defaultString)
        {
            string resourceString = GetResourceString(resourceKey);

            return resourceKey == resourceString || resourceString == null ? defaultString : resourceString;
        }

        public static string Format(string resourceFormat, object? p1)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1);
            }

            return string.Format(resourceFormat, p1);
        }

        public static string Format(string resourceFormat, object? p1, object? p2)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1, p2);
            }

            return string.Format(resourceFormat, p1, p2);
        }

        public static string Format(string resourceFormat, object? p1, object? p2, object? p3)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1, p2, p3);
            }

            return string.Format(resourceFormat, p1, p2, p3);
        }

        public static string Format(string resourceFormat, params object?[]? args)
        {
            if (args != null)
            {
                if (UsingResourceKeys())
                {
                    return resourceFormat + ", " + string.Join(", ", args);
                }

                return string.Format(resourceFormat, args);
            }

            return resourceFormat;
        }

        public static string Format(IFormatProvider? provider, string resourceFormat, object? p1)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1);
            }

            return string.Format(provider, resourceFormat, p1);
        }

        public static string Format(IFormatProvider? provider, string resourceFormat, object? p1, object? p2)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1, p2);
            }

            return string.Format(provider, resourceFormat, p1, p2);
        }

        public static string Format(IFormatProvider? provider, string resourceFormat, object? p1, object? p2, object? p3)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1, p2, p3);
            }

            return string.Format(provider, resourceFormat, p1, p2, p3);
        }

        public static string Format(IFormatProvider? provider, string resourceFormat, params object?[]? args)
        {
            if (args != null)
            {
                if (UsingResourceKeys())
                {
                    return resourceFormat + ", " + string.Join(", ", args);
                }

                return string.Format(provider, resourceFormat, args);
            }

            return resourceFormat;
        }
    }

    public static partial class SR
    {
        private static global::System.Resources.ResourceManager s_resourceManager;
        public static global::System.Resources.ResourceManager ResourceManager => s_resourceManager ?? (s_resourceManager = new global::System.Resources.ResourceManager(typeof(FxResources.System.Reflection.Metadata.SR)));

        /// <summary>Image is too small.</summary>
        public static string @ImageTooSmall => GetResourceString("ImageTooSmall", @"Image is too small.");
        /// <summary>Invalid COR header size.</summary>
        public static string @InvalidCorHeaderSize => GetResourceString("InvalidCorHeaderSize", @"Invalid COR header size.");
        /// <summary>Invalid handle.</summary>
        public static string @InvalidHandle => GetResourceString("InvalidHandle", @"Invalid handle.");
        /// <summary>Unexpected handle kind: {0}.</summary>
        public static string @UnexpectedHandleKind => GetResourceString("UnexpectedHandleKind", @"Unexpected handle kind: {0}.");
        /// <summary>Unexpected op-code: {0}.</summary>
        public static string @UnexpectedOpCode => GetResourceString("UnexpectedOpCode", @"Unexpected op-code: {0}.");
        /// <summary>Invalid local signature token: 0x{0:X8}</summary>
        public static string @InvalidLocalSignatureToken => GetResourceString("InvalidLocalSignatureToken", @"Invalid local signature token: 0x{0:X8}");
        /// <summary>Invalid metadata section span.</summary>
        public static string @InvalidMetadataSectionSpan => GetResourceString("InvalidMetadataSectionSpan", @"Invalid metadata section span.");
        /// <summary>Invalid method header: 0x{0:X2}</summary>
        public static string @InvalidMethodHeader1 => GetResourceString("InvalidMethodHeader1", @"Invalid method header: 0x{0:X2}");
        /// <summary>Invalid method header: 0x{0:X2} 0x{1:X2}</summary>
        public static string @InvalidMethodHeader2 => GetResourceString("InvalidMethodHeader2", @"Invalid method header: 0x{0:X2} 0x{1:X2}");
        /// <summary>Invalid PE signature.</summary>
        public static string @InvalidPESignature => GetResourceString("InvalidPESignature", @"Invalid PE signature.");
        /// <summary>Invalid SEH header: 0x{0:X2}</summary>
        public static string @InvalidSehHeader => GetResourceString("InvalidSehHeader", @"Invalid SEH header: 0x{0:X2}");
        /// <summary>Invalid token.</summary>
        public static string @InvalidToken => GetResourceString("InvalidToken", @"Invalid token.");
        /// <summary>Metadata image doesn't represent an assembly.</summary>
        public static string @MetadataImageDoesNotRepresentAnAssembly => GetResourceString("MetadataImageDoesNotRepresentAnAssembly", @"Metadata image doesn't represent an assembly.");
        /// <summary>Standalone debug metadata image doesn't contain Module table.</summary>
        public static string @StandaloneDebugMetadataImageDoesNotContainModuleTable => GetResourceString("StandaloneDebugMetadataImageDoesNotContainModuleTable", @"Standalone debug metadata image doesn't contain Module table.");
        /// <summary>PE image not available.</summary>
        public static string @PEImageNotAvailable => GetResourceString("PEImageNotAvailable", @"PE image not available.");
        /// <summary>Missing data directory.</summary>
        public static string @MissingDataDirectory => GetResourceString("MissingDataDirectory", @"Missing data directory.");
        /// <summary>Specified handle is not a valid metadata heap handle.</summary>
        public static string @NotMetadataHeapHandle => GetResourceString("NotMetadataHeapHandle", @"Specified handle is not a valid metadata heap handle.");
        /// <summary>Specified handle is not a valid metadata table or UserString heap handle.</summary>
        public static string @NotMetadataTableOrUserStringHandle => GetResourceString("NotMetadataTableOrUserStringHandle", @"Specified handle is not a valid metadata table or UserString heap handle.");
        /// <summary>Section too small.</summary>
        public static string @SectionTooSmall => GetResourceString("SectionTooSmall", @"Section too small.");
        /// <summary>Stream must support read and seek operations.</summary>
        public static string @StreamMustSupportReadAndSeek => GetResourceString("StreamMustSupportReadAndSeek", @"Stream must support read and seek operations.");
        /// <summary>Unknown file format.</summary>
        public static string @UnknownFileFormat => GetResourceString("UnknownFileFormat", @"Unknown file format.");
        /// <summary>Unknown PE Magic value.</summary>
        public static string @UnknownPEMagicValue => GetResourceString("UnknownPEMagicValue", @"Unknown PE Magic value.");
        /// <summary>Metadata table {0} not sorted.</summary>
        public static string @MetadataTableNotSorted => GetResourceString("MetadataTableNotSorted", @"Metadata table {0} not sorted.");
        /// <summary>Invalid number of rows of Module table: {0}.</summary>
        public static string @ModuleTableInvalidNumberOfRows => GetResourceString("ModuleTableInvalidNumberOfRows", @"Invalid number of rows of Module table: {0}.");
        /// <summary>Unknown tables: 0x{0:x16}.</summary>
        public static string @UnknownTables => GetResourceString("UnknownTables", @"Unknown tables: 0x{0:x16}.");
        /// <summary>Illegal tables in compressed metadata stream.</summary>
        public static string @IllegalTablesInCompressedMetadataStream => GetResourceString("IllegalTablesInCompressedMetadataStream", @"Illegal tables in compressed metadata stream.");
        /// <summary>Table row count space to small.</summary>
        public static string @TableRowCountSpaceTooSmall => GetResourceString("TableRowCountSpaceTooSmall", @"Table row count space to small.");
        /// <summary>Read out of bounds.</summary>
        public static string @OutOfBoundsRead => GetResourceString("OutOfBoundsRead", @"Read out of bounds.");
        /// <summary>Write out of bounds.</summary>
        public static string @OutOfBoundsWrite => GetResourceString("OutOfBoundsWrite", @"Write out of bounds.");
        /// <summary>Metadata header too small.</summary>
        public static string @MetadataHeaderTooSmall => GetResourceString("MetadataHeaderTooSmall", @"Metadata header too small.");
        /// <summary>Invalid COR20 header signature.</summary>
        public static string @MetadataSignature => GetResourceString("MetadataSignature", @"Invalid COR20 header signature.");
        /// <summary>Not enough space for version string.</summary>
        public static string @NotEnoughSpaceForVersionString => GetResourceString("NotEnoughSpaceForVersionString", @"Not enough space for version string.");
        /// <summary>Stream header too small.</summary>
        public static string @StreamHeaderTooSmall => GetResourceString("StreamHeaderTooSmall", @"Stream header too small.");
        /// <summary>Not enough space for stream header name.</summary>
        public static string @NotEnoughSpaceForStreamHeaderName => GetResourceString("NotEnoughSpaceForStreamHeaderName", @"Not enough space for stream header name.");
        /// <summary>Not enough space for String stream.</summary>
        public static string @NotEnoughSpaceForStringStream => GetResourceString("NotEnoughSpaceForStringStream", @"Not enough space for String stream.");
        /// <summary>Not enough space for Blob stream.</summary>
        public static string @NotEnoughSpaceForBlobStream => GetResourceString("NotEnoughSpaceForBlobStream", @"Not enough space for Blob stream.");
        /// <summary>Not enough space for GUID stream.</summary>
        public static string @NotEnoughSpaceForGUIDStream => GetResourceString("NotEnoughSpaceForGUIDStream", @"Not enough space for GUID stream.");
        /// <summary>Not enough space for Metadata stream.</summary>
        public static string @NotEnoughSpaceForMetadataStream => GetResourceString("NotEnoughSpaceForMetadataStream", @"Not enough space for Metadata stream.");
        /// <summary>Invalid Metadata stream format.</summary>
        public static string @InvalidMetadataStreamFormat => GetResourceString("InvalidMetadataStreamFormat", @"Invalid Metadata stream format.");
        /// <summary>Metadata tables too small.</summary>
        public static string @MetadataTablesTooSmall => GetResourceString("MetadataTablesTooSmall", @"Metadata tables too small.");
        /// <summary>Metadata table header too small.</summary>
        public static string @MetadataTableHeaderTooSmall => GetResourceString("MetadataTableHeaderTooSmall", @"Metadata table header too small.");
        /// <summary>Missing mscorlib reference in AssemblyRef table.</summary>
        public static string @WinMDMissingMscorlibRef => GetResourceString("WinMDMissingMscorlibRef", @"Missing mscorlib reference in AssemblyRef table.");
        /// <summary>Unexpected stream end.</summary>
        public static string @UnexpectedStreamEnd => GetResourceString("UnexpectedStreamEnd", @"Unexpected stream end.");
        /// <summary>Invalid relative virtual address (RVA): 0x{0:X8}</summary>
        public static string @InvalidMethodRva => GetResourceString("InvalidMethodRva", @"Invalid relative virtual address (RVA): 0x{0:X8}");
        /// <summary>Can't get a heap offset for a virtual heap handle</summary>
        public static string @CantGetOffsetForVirtualHeapHandle => GetResourceString("CantGetOffsetForVirtualHeapHandle", @"Can't get a heap offset for a virtual heap handle");
        /// <summary>Invalid number of sections declared in PE header.</summary>
        public static string @InvalidNumberOfSections => GetResourceString("InvalidNumberOfSections", @"Invalid number of sections declared in PE header.");
        /// <summary>Invalid signature.</summary>
        public static string @InvalidSignature => GetResourceString("InvalidSignature", @"Invalid signature.");
        /// <summary>PE image does not have metadata.</summary>
        public static string @PEImageDoesNotHaveMetadata => GetResourceString("PEImageDoesNotHaveMetadata", @"PE image does not have metadata.");
        /// <summary>Invalid coded index.</summary>
        public static string @InvalidCodedIndex => GetResourceString("InvalidCodedIndex", @"Invalid coded index.");
        /// <summary>Invalid compressed integer.</summary>
        public static string @InvalidCompressedInteger => GetResourceString("InvalidCompressedInteger", @"Invalid compressed integer.");
        /// <summary>Invalid document name.</summary>
        public static string @InvalidDocumentName => GetResourceString("InvalidDocumentName", @"Invalid document name.");
        /// <summary>Row ID or heap offset is too large.</summary>
        public static string @RowIdOrHeapOffsetTooLarge => GetResourceString("RowIdOrHeapOffsetTooLarge", @"Row ID or heap offset is too large.");
        /// <summary>EnCMap table not sorted or has missing records.</summary>
        public static string @EnCMapNotSorted => GetResourceString("EnCMapNotSorted", @"EnCMap table not sorted or has missing records.");
        /// <summary>Invalid serialized string.</summary>
        public static string @InvalidSerializedString => GetResourceString("InvalidSerializedString", @"Invalid serialized string.");
        /// <summary>Stream length minus starting position is too large to hold a PEImage.</summary>
        public static string @StreamTooLarge => GetResourceString("StreamTooLarge", @"Stream length minus starting position is too large to hold a PEImage.");
        /// <summary>Image is either too small or contains an invalid byte offset or count.</summary>
        public static string @ImageTooSmallOrContainsInvalidOffsetOrCount => GetResourceString("ImageTooSmallOrContainsInvalidOffsetOrCount", @"Image is either too small or contains an invalid byte offset or count.");
        /// <summary>The MetadataStringDecoder instance used to instantiate the Metadata reader must have a UTF8 encoding.</summary>
        public static string @MetadataStringDecoderEncodingMustBeUtf8 => GetResourceString("MetadataStringDecoderEncodingMustBeUtf8", @"The MetadataStringDecoder instance used to instantiate the Metadata reader must have a UTF8 encoding.");
        /// <summary>Invalid constant value.</summary>
        public static string @InvalidConstantValue => GetResourceString("InvalidConstantValue", @"Invalid constant value.");
        /// <summary>Value of type '{0}' is not a constant.</summary>
        public static string @InvalidConstantValueOfType => GetResourceString("InvalidConstantValueOfType", @"Value of type '{0}' is not a constant.");
        /// <summary>Invalid import definition kind: {0}.</summary>
        public static string @InvalidImportDefinitionKind => GetResourceString("InvalidImportDefinitionKind", @"Invalid import definition kind: {0}.");
        /// <summary>Value is too large.</summary>
        public static string @ValueTooLarge => GetResourceString("ValueTooLarge", @"Value is too large.");
        /// <summary>Blob is to large.</summary>
        public static string @BlobTooLarge => GetResourceString("BlobTooLarge", @"Blob is to large.");
        /// <summary>Invalid type size.</summary>
        public static string @InvalidTypeSize => GetResourceString("InvalidTypeSize", @"Invalid type size.");
        /// <summary>Handle belongs to a future generation</summary>
        public static string @HandleBelongsToFutureGeneration => GetResourceString("HandleBelongsToFutureGeneration", @"Handle belongs to a future generation");
        /// <summary>Invalid row count: {0}</summary>
        public static string @InvalidRowCount => GetResourceString("InvalidRowCount", @"Invalid row count: {0}");
        /// <summary>Invalid entry point token: 0x{0:8X}</summary>
        public static string @InvalidEntryPointToken => GetResourceString("InvalidEntryPointToken", @"Invalid entry point token: 0x{0:8X}");
        /// <summary>There are too many subnamespaces.</summary>
        public static string @TooManySubnamespaces => GetResourceString("TooManySubnamespaces", @"There are too many subnamespaces.");
        /// <summary>There are too many exception regions.</summary>
        public static string @TooManyExceptionRegions => GetResourceString("TooManyExceptionRegions", @"There are too many exception regions.");
        /// <summary>Sequence point value is out of range.</summary>
        public static string @SequencePointValueOutOfRange => GetResourceString("SequencePointValueOutOfRange", @"Sequence point value is out of range.");
        /// <summary>Invalid directory relative virtual address.</summary>
        public static string @InvalidDirectoryRVA => GetResourceString("InvalidDirectoryRVA", @"Invalid directory relative virtual address.");
        /// <summary>Invalid directory size.</summary>
        public static string @InvalidDirectorySize => GetResourceString("InvalidDirectorySize", @"Invalid directory size.");
        /// <summary>The value of field Characteristics in debug directory entry must be zero.</summary>
        public static string @InvalidDebugDirectoryEntryCharacteristics => GetResourceString("InvalidDebugDirectoryEntryCharacteristics", @"The value of field Characteristics in debug directory entry must be zero.");
        /// <summary>Unexpected CodeView data signature value.</summary>
        public static string @UnexpectedCodeViewDataSignature => GetResourceString("UnexpectedCodeViewDataSignature", @"Unexpected CodeView data signature value.");
        /// <summary>Unexpected Embedded Portable PDB data signature value.</summary>
        public static string @UnexpectedEmbeddedPortablePdbDataSignature => GetResourceString("UnexpectedEmbeddedPortablePdbDataSignature", @"Unexpected Embedded Portable PDB data signature value.");
        /// <summary>Invalid PDB Checksum data format.</summary>
        public static string @InvalidPdbChecksumDataFormat => GetResourceString("InvalidPdbChecksumDataFormat", @"Invalid PDB Checksum data format.");
        /// <summary>Expected signature header for '{0}', but found '{1}' (0x{2:x2}).</summary>
        public static string @UnexpectedSignatureHeader => GetResourceString("UnexpectedSignatureHeader", @"Expected signature header for '{0}', but found '{1}' (0x{2:x2}).");
        /// <summary>Expected signature header for '{0}' or '{1}', but found '{2}' (0x{3:x2}).</summary>
        public static string @UnexpectedSignatureHeader2 => GetResourceString("UnexpectedSignatureHeader2", @"Expected signature header for '{0}' or '{1}', but found '{2}' (0x{3:x2}).");
        /// <summary>Specified handle is not a TypeDefinitionHandle or TypeReferenceHandle.</summary>
        public static string @NotTypeDefOrRefHandle => GetResourceString("NotTypeDefOrRefHandle", @"Specified handle is not a TypeDefinitionHandle or TypeReferenceHandle.");
        /// <summary>Unexpected SignatureTypeCode: (0x{0:x}).</summary>
        public static string @UnexpectedSignatureTypeCode => GetResourceString("UnexpectedSignatureTypeCode", @"Unexpected SignatureTypeCode: (0x{0:x}).");
        /// <summary>Signature type sequence must have at least one element.</summary>
        public static string @SignatureTypeSequenceMustHaveAtLeastOneElement => GetResourceString("SignatureTypeSequenceMustHaveAtLeastOneElement", @"Signature type sequence must have at least one element.");
        /// <summary>Specified handle is not a TypeDefinitionHandle, TypeReferenceHandle, or TypeSpecificationHandle.</summary>
        public static string @NotTypeDefOrRefOrSpecHandle => GetResourceString("NotTypeDefOrRefOrSpecHandle", @"Specified handle is not a TypeDefinitionHandle, TypeReferenceHandle, or TypeSpecificationHandle.");
        /// <summary>The Debug directory was not of type {0}.</summary>
        public static string @UnexpectedDebugDirectoryType => GetResourceString("UnexpectedDebugDirectoryType", @"The Debug directory was not of type {0}.");
        /// <summary>The limit on the size of {0} heap has been exceeded.</summary>
        public static string @HeapSizeLimitExceeded => GetResourceString("HeapSizeLimitExceeded", @"The limit on the size of {0} heap has been exceeded.");
        /// <summary>Builder must be aligned to 4 byte boundary.</summary>
        public static string @BuilderMustAligned => GetResourceString("BuilderMustAligned", @"Builder must be aligned to 4 byte boundary.");
        /// <summary>The operation is not valid on this builder as it has been linked with another one.</summary>
        public static string @BuilderAlreadyLinked => GetResourceString("BuilderAlreadyLinked", @"The operation is not valid on this builder as it has been linked with another one.");
        /// <summary>The size of the builder returned by {0}.{1} is smaller than requested.</summary>
        public static string @ReturnedBuilderSizeTooSmall => GetResourceString("ReturnedBuilderSizeTooSmall", @"The size of the builder returned by {0}.{1} is smaller than requested.");
        /// <summary>Can't add vararg parameters to non-vararg signature.</summary>
        public static string @SignatureNotVarArg => GetResourceString("SignatureNotVarArg", @"Can't add vararg parameters to non-vararg signature.");
        /// <summary>Specified label doesn't belong to the current builder.</summary>
        public static string @LabelDoesntBelongToBuilder => GetResourceString("LabelDoesntBelongToBuilder", @"Specified label doesn't belong to the current builder.");
        /// <summary>Can't emit a branch or exception region, the current encoder not created with a control flow builder.</summary>
        public static string @ControlFlowBuilderNotAvailable => GetResourceString("ControlFlowBuilderNotAvailable", @"Can't emit a branch or exception region, the current encoder not created with a control flow builder.");
        /// <summary>Base reader must be a full metadata reader.</summary>
        public static string @BaseReaderMustBeFullMetadataReader => GetResourceString("BaseReaderMustBeFullMetadataReader", @"Base reader must be a full metadata reader.");
        /// <summary>Module already added.</summary>
        public static string @ModuleAlreadyAdded => GetResourceString("ModuleAlreadyAdded", @"Module already added.");
        /// <summary>Assembly already added.</summary>
        public static string @AssemblyAlreadyAdded => GetResourceString("AssemblyAlreadyAdded", @"Assembly already added.");
        /// <summary>Expected list of size {0}.</summary>
        public static string @ExpectedListOfSize => GetResourceString("ExpectedListOfSize", @"Expected list of size {0}.");
        /// <summary>Expected array of size {0}.</summary>
        public static string @ExpectedArrayOfSize => GetResourceString("ExpectedArrayOfSize", @"Expected array of size {0}.");
        /// <summary>Expected non-empty list.</summary>
        public static string @ExpectedNonEmptyList => GetResourceString("ExpectedNonEmptyList", @"Expected non-empty list.");
        /// <summary>Expected non-empty array.</summary>
        public static string @ExpectedNonEmptyArray => GetResourceString("ExpectedNonEmptyArray", @"Expected non-empty array.");
        /// <summary>Expected non-empty string.</summary>
        public static string @ExpectedNonEmptyString => GetResourceString("ExpectedNonEmptyString", @"Expected non-empty string.");
        /// <summary>Specified readers must be minimal delta metadata readers.</summary>
        public static string @ReadersMustBeDeltaReaders => GetResourceString("ReadersMustBeDeltaReaders", @"Specified readers must be minimal delta metadata readers.");
        /// <summary>Signature provider returned invalid signature.</summary>
        public static string @SignatureProviderReturnedInvalidSignature => GetResourceString("SignatureProviderReturnedInvalidSignature", @"Signature provider returned invalid signature.");
        /// <summary>Unknown section name: '{0}'.</summary>
        public static string @UnknownSectionName => GetResourceString("UnknownSectionName", @"Unknown section name: '{0}'.");
        /// <summary>Hash must be at least {0}B long.</summary>
        public static string @HashTooShort => GetResourceString("HashTooShort", @"Hash must be at least {0}B long.");
        /// <summary>Expected array of length {0}.</summary>
        public static string @UnexpectedArrayLength => GetResourceString("UnexpectedArrayLength", @"Expected array of length {0}.");
        /// <summary>Value must be multiple of {0}.</summary>
        public static string @ValueMustBeMultiple => GetResourceString("ValueMustBeMultiple", @"Value must be multiple of {0}.");
        /// <summary>{0} must not return null.</summary>
        public static string @MustNotReturnNull => GetResourceString("MustNotReturnNull", @"{0} must not return null.");
        /// <summary>Metadata version too long.</summary>
        public static string @MetadataVersionTooLong => GetResourceString("MetadataVersionTooLong", @"Metadata version too long.");
        /// <summary>Row count must be zero for table #{0}.</summary>
        public static string @RowCountMustBeZero => GetResourceString("RowCountMustBeZero", @"Row count must be zero for table #{0}.");
        /// <summary>Row count specified for table index {0} is out of allowed range.</summary>
        public static string @RowCountOutOfRange => GetResourceString("RowCountOutOfRange", @"Row count specified for table index {0} is out of allowed range.");
        /// <summary>Declared size doesn't correspond to the actual size.</summary>
        public static string @SizeMismatch => GetResourceString("SizeMismatch", @"Declared size doesn't correspond to the actual size.");
        /// <summary>Data too big to fit in memory.</summary>
        public static string @DataTooBig => GetResourceString("DataTooBig", @"Data too big to fit in memory.");
        /// <summary>Unsupported format version: {0}</summary>
        public static string @UnsupportedFormatVersion => GetResourceString("UnsupportedFormatVersion", @"Unsupported format version: {0}");
        /// <summary>The distance between the instruction {0} (offset {1}) and the target label doesn't fit the operand size: {2}</summary>
        public static string @DistanceBetweenInstructionAndLabelTooBig => GetResourceString("DistanceBetweenInstructionAndLabelTooBig", @"The distance between the instruction {0} (offset {1}) and the target label doesn't fit the operand size: {2}");
        /// <summary>Label {0} has not been marked.</summary>
        public static string @LabelNotMarked => GetResourceString("LabelNotMarked", @"Label {0} has not been marked.");
        /// <summary>Method body was created with no exception regions.</summary>
        public static string @MethodHasNoExceptionRegions => GetResourceString("MethodHasNoExceptionRegions", @"Method body was created with no exception regions.");
        /// <summary>Invalid exception region bounds: start offset ({0}) is greater than end offset ({1}).</summary>
        public static string @InvalidExceptionRegionBounds => GetResourceString("InvalidExceptionRegionBounds", @"Invalid exception region bounds: start offset ({0}) is greater than end offset ({1}).");
        /// <summary>Unexpected value '{0}' of type '{1}'</summary>
        public static string @UnexpectedValue => GetResourceString("UnexpectedValue", @"Unexpected value '{0}' of type '{1}'");
        /// <summary>Unexpected value '{0}' of unknown type.</summary>
        public static string @UnexpectedValueUnknownType => GetResourceString("UnexpectedValueUnknownType", @"Unexpected value '{0}' of unknown type.");
        /// <summary>The SwitchInstructionEncoder.Branch method was invoked too few times.</summary>
        public static string @SwitchInstructionEncoderTooFewBranches => GetResourceString("SwitchInstructionEncoderTooFewBranches", @"The SwitchInstructionEncoder.Branch method was invoked too few times.");
        /// <summary>The SwitchInstructionEncoder.Branch method was invoked too many times.</summary>
        public static string @SwitchInstructionEncoderTooManyBranches => GetResourceString("SwitchInstructionEncoderTooManyBranches", @"The SwitchInstructionEncoder.Branch method was invoked too many times.");

        public static string CollectionModifiedDuringEnumeration => GetResourceString("CollectionModifiedDuringEnumeration", "Collection was modified; enumeration operation may not execute.");

        public static string DuplicateKey => GetResourceString("DuplicateKey",
            "An element with the same key but a different value already exists. Key: {0}");

        public static string Arg_KeyNotFoundWithKey => GetResourceString("KeyNotFoundWithKey",
            "The key has not found in the dictionary. Key: {0}");
    }
}