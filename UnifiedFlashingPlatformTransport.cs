/*
* MIT License
* 
* Copyright (c) 2024 The DuoWOA authors
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/
using MadWizard.WinUSBNet;
using System;
using System.Linq;
using System.Text;

namespace UnifiedFlashingPlatform
{
    public partial class UnifiedFlashingPlatformTransport : IDisposable
    {
        private bool Disposed = false;
        private readonly USBDevice USBDevice = null;
        private USBPipe InputPipe = null;
        private USBPipe OutputPipe = null;
        private object UsbLock = new();

        public UnifiedFlashingPlatformTransport(string DevicePath)
        {
            USBDevice = new USBDevice(DevicePath);

            foreach (USBPipe Pipe in USBDevice.Pipes)
            {
                if (Pipe.IsIn)
                {
                    InputPipe = Pipe;
                }

                if (Pipe.IsOut)
                {
                    OutputPipe = Pipe;
                }
            }

            if (InputPipe == null || OutputPipe == null)
            {
                throw new Exception("Invalid USB device!");
            }
        }

        public byte[] ExecuteRawMethod(byte[] RawMethod)
        {
            return ExecuteRawMethod(RawMethod, RawMethod.Length);
        }

        public byte[] ExecuteRawMethod(byte[] RawMethod, int Length)
        {
            byte[] Buffer = new byte[0xF000]; // Should be at least 0x4408 for receiving the GPT packet.
            byte[] Result = null;
            lock (UsbLock)
            {
                OutputPipe.Write(RawMethod, 0, Length);
                try
                {
                    int OutputLength = InputPipe.Read(Buffer);
                    Result = new byte[OutputLength];
                    System.Buffer.BlockCopy(Buffer, 0, Result, 0, OutputLength);
                }
                catch { } // Reboot command looses connection
            }
            return Result;
        }

        public void ExecuteRawVoidMethod(byte[] RawMethod)
        {
            ExecuteRawVoidMethod(RawMethod, RawMethod.Length);
        }

        public void ExecuteRawVoidMethod(byte[] RawMethod, int Length)
        {
            lock (UsbLock)
            {
                OutputPipe.Write(RawMethod, 0, Length);
            }
        }

        public byte[] ReadParam(string Param)
        {
            byte[] Request = new byte[0x0B];
            string Header = ReadParamSignature; // NOKXFR

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Param), 0, Request, 7, Param.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 0x10))
            {
                return null;
            }

            byte[] Result = new byte[Response[0x10]];
            Buffer.BlockCopy(Response, 0x11, Result, 0, Response[0x10]);
            return Result;
        }

        public string ReadStringParam(string Param)
        {
            Console.WriteLine($"Reading {Param}");
            byte[] Bytes = ReadParam(Param);
            if (Bytes == null)
            {
                return null;
            }

            string result = Encoding.ASCII.GetString(Bytes).Trim('\0');

            Console.WriteLine($"Result (as  bytes): {BitConverter.ToString(Bytes).Replace("-", "")}");
            Console.WriteLine($"Result (fl string): {Encoding.ASCII.GetString(Bytes).Replace("\0", "\\0")}");
            Console.WriteLine($"Result (as string): {result}");

            return result;
        }

        public enum FlashAppType
        {
            UFP = 1,
            Unknown
        };

        public FlashAppType ReadAppType()
        {
            byte[] Bytes = ReadParam("APPT");
            if (Bytes == null)
            {
                return FlashAppType.Unknown;
            }

            if (Bytes[0] == 1)
            {
                return FlashAppType.UFP;
            }

            return FlashAppType.Unknown;
        }

        public struct ResetProtectionInfo
        {
            public bool IsResetProtectionEnabled;
            public uint MajorVersion;
            public uint MinorVersion;

            public override string ToString()
            {
                return "IsResetProtectionEnabled: " + IsResetProtectionEnabled + 
                    " - MajorVersion: " + MajorVersion + 
                    " - MinorVersion: " + MinorVersion;
            }
        }

        public ResetProtectionInfo? ReadResetProtection()
        {
            byte[] Bytes = ReadParam("ATRP");
            if (Bytes == null)
            {
                return null;
            }

            return new ResetProtectionInfo()
            {
                IsResetProtectionEnabled = Bytes[0] == 1,
                MajorVersion = BitConverter.ToUInt32(Bytes[1..5].Reverse().ToArray()),
                MinorVersion = BitConverter.ToUInt32(Bytes[5..9].Reverse().ToArray())
            };
        }

        public bool? ReadBitlocker()
        {
            byte[] Bytes = ReadParam("BITL");
            if (Bytes == null)
            {
                return null;
            }

            return Bytes[0] == 1;
        }

        public string ReadBuildInfo()
        {
            return ReadStringParam("BNFO");
        }

        public ushort? ReadCurrentBootOption()
        {
            byte[] Bytes = ReadParam("CUFO");
            if (Bytes == null)
            {
                return null;
            }

            return BitConverter.ToUInt16(Bytes.Reverse().ToArray());
        }

        public bool? ReadDeviceAsyncSupport()
        {
            byte[] Bytes = ReadParam("DAS\0");
            if (Bytes == null)
            {
                return null;
            }

            return BitConverter.ToUInt16(Bytes.Reverse().ToArray()) == 1;
        }

        public UInt64? ReadDirectoryEntriesSize(string PartitionName, string FileName)
        {
            if (PartitionName.Length > 35)
            {
                return null;
            }

            byte[] PartitionNameBuffer = Encoding.Unicode.GetBytes(PartitionName);
            byte[] FileNameBuffer = Encoding.Unicode.GetBytes(FileName);

            byte[] Request = new byte[87 + FileNameBuffer.Length + 2];
            string Header = ReadParamSignature; // NOKXFR
            const string Param = "DES\0";

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Param), 0, Request, 7, Param.Length);

            Buffer.BlockCopy(PartitionNameBuffer, 0, Request, 15, PartitionNameBuffer.Length);
            Buffer.BlockCopy(FileNameBuffer, 0, Request, 87, FileNameBuffer.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 0x10))
            {
                return null;
            }

            byte[] Result = new byte[Response[0x10]];
            Buffer.BlockCopy(Response, 0x11, Result, 0, Response[0x10]);

            return BitConverter.ToUInt64(Result.Reverse().ToArray());
        }

        public string ReadDevicePlatformID()
        {
            return ReadStringParam("DPI\0");
        }

        //
        // Reads the device properties from the UEFI Variable "MSRuntimeDeviceProperties"
        // in the g_guidMSRuntimeDeviceProperties namespace and returns it as a string.
        //
        public string ReadDeviceProperties()
        {
            return ReadStringParam("DPR\0");
        }

        public struct DeviceTargetingInfo
        {
            public string Manufacturer;
            public string Family;
            public string ProductName;
            public string ProductVersion;
            public string SKUNumber;
            public string BaseboardManufacturer;
            public string BaseboardProduct;

            public override string ToString()
            {
                return "Manufacturer: " + Manufacturer + 
                    " - Family: " + Family + 
                    " - Product Name: " + ProductName + 
                    " - Product Version: " + ProductVersion + 
                    " - SKU Number: " + SKUNumber + 
                    " - Baseboard Manufacturer: " + BaseboardManufacturer + 
                    " - Baseboard Product: " + BaseboardProduct;
            }
        }

        public DeviceTargetingInfo? ReadDeviceTargetInfo()
        {
            byte[] Bytes = ReadParam("DTI\0");
            if (Bytes == null)
            {
                return null;
            }

            UInt16 ManufacturerLength = BitConverter.ToUInt16(Bytes[0..2].Reverse().ToArray());
            UInt16 FamilyLength = BitConverter.ToUInt16(Bytes[2..4].Reverse().ToArray());
            UInt16 ProductNameLength = BitConverter.ToUInt16(Bytes[4..6].Reverse().ToArray());
            UInt16 ProductVersionLength = BitConverter.ToUInt16(Bytes[6..8].Reverse().ToArray());
            UInt16 SKUNumberLength = BitConverter.ToUInt16(Bytes[8..10].Reverse().ToArray());
            UInt16 BaseboardManufacturerLength = BitConverter.ToUInt16(Bytes[10..12].Reverse().ToArray());
            UInt16 BaseboardProductLength = BitConverter.ToUInt16(Bytes[12..14].Reverse().ToArray());

            Int32 CurrentOffset = 14;
            string Manufacturer = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + ManufacturerLength)]);

            CurrentOffset += ManufacturerLength;
            string Family = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + FamilyLength)]);

            CurrentOffset += FamilyLength;
            string ProductName = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + ProductNameLength)]);

            CurrentOffset += ProductNameLength;
            string ProductVersion = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + ProductVersionLength)]);

            CurrentOffset += ProductVersionLength;
            string SKUNumber = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + SKUNumberLength)]);

            CurrentOffset += SKUNumberLength;
            string BaseboardManufacturer = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + BaseboardManufacturerLength)]);

            CurrentOffset += BaseboardManufacturerLength;
            string BaseboardProduct = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + BaseboardProductLength)]);

            return new DeviceTargetingInfo()
            {
                Manufacturer = Manufacturer,
                Family = Family,
                ProductName = ProductName,
                ProductVersion = ProductVersion,
                SKUNumber = SKUNumber,
                BaseboardManufacturer = BaseboardManufacturer,
                BaseboardProduct = BaseboardProduct
            };
        }

        public string ReadDataVerifySpeed()
        {
            return ReadStringParam("DTSP");
        }

        public string ReadDeviceID()
        {
            return ReadStringParam("DUI\0");
        }

        public string ReadEmmcTestResult()
        {
            return ReadStringParam("EMMT");
        }

        public string ReadEmmcSize()
        {
            return ReadStringParam("EMS\0");
        }

        public string ReadEmmcWriteSpeed()
        {
            return ReadStringParam("EMWS");
        }

        public string ReadFlashAppInfo()
        {
            return ReadStringParam("FAI\0");
        }

        public string ReadFlashOptions()
        {
            return ReadStringParam("FO\0\0");
        }

        public string ReadFlashingStatus()
        {
            return ReadStringParam("FS\0\0");
        }

        public string ReadFileSize()
        {
            return ReadStringParam("FZ\0\0");
        }

        public string ReadSecureBootStatus()
        {
            return ReadStringParam("GSBS");
        }

        public string ReadUEFIVariable()
        {
            return ReadStringParam("GUFV");
        }

        public string ReadUEFIVariableSize()
        {
            return ReadStringParam("GUVS");
        }

        public string ReadLargestMemoryRegion()
        {
            return ReadStringParam("LGMR");
        }

        public string ReadLogSize()
        {
            return ReadStringParam("LZ\0\0");
        }

        public string ReadMacAddress()
        {
            return ReadStringParam("MAC\0");
        }

        public string ReadModeData()
        {
            return ReadStringParam("MODE");
        }

        public string ReadProcessorManufacturer()
        {
            return ReadStringParam("pm\0\0");
        }

        public string ReadSDCardSize()
        {
            return ReadStringParam("SDS\0");
        }

        public string ReadSupportedFFUProtocolInfo()
        {
            return ReadStringParam("SFPI");
        }

        public string ReadSMBIOSData()
        {
            return ReadStringParam("SMBD");
        }

        public string ReadSerialNumber()
        {
            return ReadStringParam("SN\0\0");
        }

        public string ReadSizeOfSystemMemory()
        {
            return ReadStringParam("SOSM");
        }

        public string ReadSecurityStatus()
        {
            return ReadStringParam("SS\0\0");
        }

        public string ReadTelemetryLogSize()
        {
            return ReadStringParam("TELS");
        }

        public string ReadTransferSize()
        {
            return ReadStringParam("TS\0\0");
        }

        public string ReadUEFIBootFlag()
        {
            return ReadStringParam("UBF\0");
        }

        public string ReadUEFIBootOptions()
        {
            return ReadStringParam("UEBO");
        }

        public string ReadUnlockID()
        {
            return ReadStringParam("UKID");
        }

        public string ReadUnlockTokenFiles()
        {
            return ReadStringParam("UKTF");
        }

        public string ReadUSBSpeed()
        {
            return ReadStringParam("USBS");
        }

        public string ReadWriteBufferSize()
        {
            return ReadStringParam("WBS\0");
        }

        public void Relock()
        {
            byte[] Request = new byte[7];
            string Header = RelockSignature; // NOKXFO
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawMethod(Request);
        }

        public void MassStorage()
        {
            byte[] Request = new byte[7];
            string Header = MassStorageSignature; // NOKM
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawMethod(Request);
        }

        public void RebootPhone()
        {
            byte[] Request = new byte[7];
            string Header = $"{SwitchModeSignature}R"; // NOKXCBR
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawMethod(Request);
        }

        public void SwitchToUFP()
        {
            byte[] Request = new byte[7];
            string Header = $"{SwitchModeSignature}U"; // NOKXCBU
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawMethod(Request);
        }

        public void ContinueBoot()
        {
            byte[] Request = new byte[7];
            string Header = $"{SwitchModeSignature}W"; // NOKXCBW
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawMethod(Request);
        }

        public void PowerOff()
        {
            byte[] Request = new byte[7];
            string Header = $"{SwitchModeSignature}Z"; // NOKXCBZ
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawVoidMethod(Request);
        }

        public void TransitionToUFPBootApp()
        {
            byte[] Request = new byte[7];
            string Header = $"{SwitchModeSignature}T"; // NOKXCBT
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawVoidMethod(Request);
        }

        public void DisplayCustomMessage(string Message, ushort Row)
        {
            byte[] MessageBuffer = Encoding.Unicode.GetBytes(Message);
            byte[] Request = new byte[8 + MessageBuffer.Length];
            string Header = DisplayCustomMessageSignature; // NOKXCM

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(Row).Reverse().ToArray(), 0, Request, 6, 2);
            Buffer.BlockCopy(MessageBuffer, 0, Request, 8, MessageBuffer.Length);

            ExecuteRawMethod(Request);
        }

        public void ClearScreen()
        {
            byte[] Request = new byte[6];
            string Header = ClearScreenSignature; // NOKXCC
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawMethod(Request);
        }

        public byte[] Echo(byte[] DataPayload)
        {
            byte[] Request = new byte[10 + DataPayload.Length];
            string Header = EchoSignature; // NOKXCE

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(DataPayload.Length).Reverse().ToArray(), 0, Request, 6, 4);
            Buffer.BlockCopy(DataPayload, 0, Request, 10, DataPayload.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 6 + DataPayload.Length))
            {
                return null;
            }

            byte[] Result = new byte[DataPayload.Length];
            Buffer.BlockCopy(Response, 6, Result, 0, DataPayload.Length);
            return Result;
        }

        public void TelemetryStart()
        {
            byte[] Request = new byte[4];
            string Header = TelemetryStartSignature; // NOKS
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawVoidMethod(Request);
        }

        public void TelemetryEnd()
        {
            byte[] Request = new byte[4];
            string Header = TelemetryEndSignature; // NOKN
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawVoidMethod(Request);
        }

        public ulong GetLogSize()
        {
            byte[] Request = new byte[0x10];
            string Header = ReadParamSignature; // NOKXFR
            const string Param = "LZ\0\0";

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Param), 0, Request, 7, Param.Length);

            Request[14] = 1;
            Request[15] = 1;

            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 0x10))
            {
                return 0;
            }

            byte[] Result = new byte[Response[0x10]];
            Buffer.BlockCopy(Response, 0x11, Result, 0, Response[0x10]);

            return BitConverter.ToUInt64(Result.Reverse().ToArray(), 0);
        }

        // WIP!
        public string ReadLog()
        {
            byte[] Request = new byte[0x13];
            string Header = GetLogsSignature;
            ulong BufferSize = 0xF000 - 0xC;

            ulong Length = GetLogSize();
            if (Length == 0)
            {
                return null;
            }

            string LogContent = "";

            for (ulong i = 0; i < Length; i += BufferSize)
            {
                if (i + BufferSize > Length)
                {
                    BufferSize = Length - i;
                }
                uint BufferSizeInt = (uint)BufferSize;

                Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
                Request[6] = 1;
                Buffer.BlockCopy(BitConverter.GetBytes(BufferSizeInt).Reverse().ToArray(), 0, Request, 7, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(i).Reverse().ToArray(), 0, Request, 11, 8);

                byte[] Response = ExecuteRawMethod(Request);
                if ((Response == null) || (Response.Length < 0xC))
                {
                    return null;
                }

                int ResultLength = Response.Length - 0xC;
                byte[] Result = new byte[ResultLength];
                Buffer.BlockCopy(Response, 0xC, Result, 0, ResultLength);

                string PartialLogContent = Encoding.ASCII.GetString(Result);

                LogContent += PartialLogContent;
            }

            return LogContent;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~UnifiedFlashingPlatformTransport()
        {
            Dispose(false);
        }

        public void Close()
        {
            USBDevice?.Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
            {
                return;
            }

            if (disposing)
            {
                // Other disposables
            }

            // Clean unmanaged resources here.
            Close();

            Disposed = true;
        }
    }
}