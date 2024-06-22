﻿using Img2Ffu.Reader.Structs;
using System.Collections.Generic;
using System.IO;

namespace Img2Ffu.Reader.Data
{
    public class ValidationDescriptor
    {
        public ValidationEntry ValidationEntry;
        public byte[] ValidationBytes;

        public ValidationDescriptor(Stream stream)
        {
            ValidationEntry = stream.ReadStructure<ValidationEntry>();

            ValidationBytes = new byte[ValidationEntry.dwByteCount];
            _ = stream.Read(ValidationBytes, 0, (int)ValidationEntry.dwByteCount);
        }

        public ValidationDescriptor(ValidationEntry validationEntry, byte[] validationBytes)
        {
            ValidationEntry = validationEntry;
            ValidationBytes = validationBytes;
            ValidationEntry.dwByteCount = (uint)validationBytes.LongLength;
        }

        public override string ToString()
        {
            return $"{{ValidationEntry: {ValidationEntry}}}";
        }

        public byte[] GetBytes()
        {
            List<byte> bytes = [];

            ValidationEntry.dwByteCount = (uint)ValidationBytes.Length;

            bytes.AddRange(ValidationEntry.GetBytes());
            bytes.AddRange(ValidationBytes);

            return [.. bytes];
        }
    }
}
