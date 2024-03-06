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
        private readonly USBDevice? USBDevice = null;
        private readonly USBPipe? InputPipe = null;
        private readonly USBPipe? OutputPipe = null;
        private readonly object UsbLock = new();

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
            byte[]? Result = null;
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
            byte[] Bytes = ReadParam(Param);
            return Bytes == null ? null : Encoding.ASCII.GetString(Bytes).Trim('\0');
        }

        public AppType ReadAppType()
        {
            byte[] Bytes = ReadParam("APPT");
            return Bytes == null ? AppType.Min : Bytes[0] == 1 ? AppType.UEFI : AppType.Min;
        }

        public ResetProtectionInfo? ReadResetProtection()
        {
            byte[] Bytes = ReadParam("ATRP");
            return Bytes == null
                ? null
                : new ResetProtectionInfo()
                {
                    IsResetProtectionEnabled = Bytes[0] == 1,
                    MajorVersion = BitConverter.ToUInt32(Bytes[1..5].Reverse().ToArray()),
                    MinorVersion = BitConverter.ToUInt32(Bytes[5..9].Reverse().ToArray())
                };
        }

        public bool? ReadBitlocker()
        {
            byte[] Bytes = ReadParam("BITL");
            return Bytes == null ? null : Bytes[0] == 1;
        }

        public string ReadBuildInfo()
        {
            return ReadStringParam("BNFO");
        }

        public ushort? ReadCurrentBootOption()
        {
            byte[] Bytes = ReadParam("CUFO");
            return Bytes == null || Bytes.Length != 2 ? null : BitConverter.ToUInt16(Bytes.Reverse().ToArray());
        }

        public bool? ReadDeviceAsyncSupport()
        {
            byte[] Bytes = ReadParam("DAS\0");
            return Bytes == null || Bytes.Length != 2 ? null : BitConverter.ToUInt16(Bytes.Reverse().ToArray()) == 1;
        }

        public ulong? ReadDirectoryEntriesSize(string PartitionName, string DirectoryName)
        {
            if (PartitionName.Length > 35)
            {
                return null;
            }

            byte[] PartitionNameBuffer = Encoding.Unicode.GetBytes(PartitionName);
            byte[] DirectoryNameBuffer = Encoding.Unicode.GetBytes(DirectoryName);

            byte[] Request = new byte[87 + DirectoryNameBuffer.Length + 2];
            string Header = ReadParamSignature; // NOKXFR
            const string Param = "DES\0";

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Param), 0, Request, 7, Param.Length);

            Buffer.BlockCopy(PartitionNameBuffer, 0, Request, 15, PartitionNameBuffer.Length);
            Buffer.BlockCopy(DirectoryNameBuffer, 0, Request, 87, DirectoryNameBuffer.Length);

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

        public DeviceTargetingInfo? ReadDeviceTargetInfo()
        {
            byte[] Bytes = ReadParam("DTI\0");
            if (Bytes == null)
            {
                return null;
            }

            ushort ManufacturerLength = BitConverter.ToUInt16(Bytes[0..2].Reverse().ToArray());
            ushort FamilyLength = BitConverter.ToUInt16(Bytes[2..4].Reverse().ToArray());
            ushort ProductNameLength = BitConverter.ToUInt16(Bytes[4..6].Reverse().ToArray());
            ushort ProductVersionLength = BitConverter.ToUInt16(Bytes[6..8].Reverse().ToArray());
            ushort SKUNumberLength = BitConverter.ToUInt16(Bytes[8..10].Reverse().ToArray());
            ushort BaseboardManufacturerLength = BitConverter.ToUInt16(Bytes[10..12].Reverse().ToArray());
            ushort BaseboardProductLength = BitConverter.ToUInt16(Bytes[12..14].Reverse().ToArray());

            int CurrentOffset = 14;
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

        //
        // Gets the last FFU Flash Operation Data verify speed in KB/s
        //
        public uint? ReadDataVerifySpeed()
        {
            byte[] Bytes = ReadParam("DTSP");
            return Bytes == null || Bytes.Length != 4 ? null : BitConverter.ToUInt32(Bytes.Reverse().ToArray());
        }

        public Guid? ReadDeviceID()
        {
            byte[] Bytes = ReadParam("DUI\0");
            return Bytes == null || Bytes.Length != 16 ? null : new Guid(Bytes);
        }

        public uint? ReadEmmcTestResult()
        {
            byte[] Bytes = ReadParam("EMMT");
            return Bytes == null || Bytes.Length != 4 ? null : BitConverter.ToUInt32(Bytes.Reverse().ToArray());
        }

        //
        // Gets the eMMC Size in sectors, if present
        //
        public uint? ReadEmmcSize()
        {
            byte[] Bytes = ReadParam("EMS\0");
            return Bytes == null || Bytes.Length != 4 ? null : BitConverter.ToUInt32(Bytes.Reverse().ToArray());
        }

        //
        // Gets the eMMC Write speed in KB/s
        //
        public uint? ReadEmmcWriteSpeed()
        {
            byte[] Bytes = ReadParam("EMWS");
            return Bytes == null || Bytes.Length != 4 ? null : BitConverter.ToUInt32(Bytes.Reverse().ToArray());
        }

        public FlashAppInfo? ReadFlashAppInfo()
        {
            byte[] Bytes = ReadParam("FAI\0");
            return Bytes == null || Bytes.Length != 6 || Bytes[0] != 2
                ? null
                : new FlashAppInfo()
                {
                    ProtocolMajorVersion = Bytes[1],
                    ProtocolMinorVersion = Bytes[2],
                    ImplementationMajorVersion = Bytes[3],
                    ImplementationMinorVersion = Bytes[4]
                };
        }

        //
        // Reads the device properties from the UEFI Variable "FfuConfigurationOptions"
        // in the g_guidLumiaGuid namespace and returns it as a string.
        //
        public string ReadFlashOptions()
        {
            return ReadStringParam("FO\0\0");
        }

        public uint? ReadFlashingStatus()
        {
            byte[] Bytes = ReadParam("FS\0\0");
            return Bytes == null || Bytes.Length != 4 ? null : BitConverter.ToUInt32(Bytes.Reverse().ToArray());
        }

        public ulong? ReadFileSize(string PartitionName, string FileName)
        {
            if (PartitionName.Length > 35)
            {
                return null;
            }

            byte[] PartitionNameBuffer = Encoding.Unicode.GetBytes(PartitionName);
            byte[] FileNameBuffer = Encoding.Unicode.GetBytes(FileName);

            byte[] Request = new byte[87 + FileNameBuffer.Length + 2];
            string Header = ReadParamSignature; // NOKXFR
            const string Param = "FZ\0\0";

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

        public bool? ReadSecureBootStatus()
        {
            byte[] Bytes = ReadParam("GSBS");
            return Bytes == null ? null : Bytes[0] == 1;
        }

        public UefiVariable? ReadUEFIVariable(Guid Guid, string Name, uint Size)
        {
            byte[] Request = new byte[39 + ((Name.Length + 1) * 2)];
            string Header = ReadParamSignature; // NOKXFR
            string Param = "GUFV";

            byte[] VariableNameBuffer = Encoding.Unicode.GetBytes(Name);

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Param), 0, Request, 7, Param.Length);

            Buffer.BlockCopy(Guid.ToByteArray(), 0, Request, 15, 16);
            Buffer.BlockCopy(BitConverter.GetBytes(Size).Reverse().ToArray(), 0, Request, 31, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((Name.Length + 1) * 2).Reverse().ToArray(), 0, Request, 35, 4);
            Buffer.BlockCopy(VariableNameBuffer, 0, Request, 39, VariableNameBuffer.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 0x10))
            {
                return null;
            }

            byte[] Result = new byte[Response[0x10]];
            Buffer.BlockCopy(Response, 0x11, Result, 0, Response[0x10]);

            return new UefiVariable()
            {
                Attributes = (UefiVariableAttributes)BitConverter.ToUInt32(Result[0..4].Reverse().ToArray()),
                DataSize = BitConverter.ToUInt32(Result[4..8].Reverse().ToArray()),
                Data = Result[8..^0]
            };
        }

        public uint? ReadUEFIVariableSize(Guid Guid, string Name)
        {
            byte[] Request = new byte[39 + ((Name.Length + 1) * 2)];
            string Header = "NOKXFR"; // NOKXFR
            string Param = "GUVS";

            byte[] VariableNameBuffer = Encoding.Unicode.GetBytes(Name);

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Param), 0, Request, 7, Param.Length);

            Buffer.BlockCopy(Guid.ToByteArray(), 0, Request, 15, 16);

            Buffer.BlockCopy(BitConverter.GetBytes((Name.Length + 1) * 2).Reverse().ToArray(), 0, Request, 35, 4);
            Buffer.BlockCopy(VariableNameBuffer, 0, Request, 39, VariableNameBuffer.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 0x10))
            {
                return null;
            }

            byte[] Result = new byte[Response[0x10]];
            Buffer.BlockCopy(Response, 0x11, Result, 0, Response[0x10]);
            return Result == null || Result.Length != 4 ? null : BitConverter.ToUInt32(Result.Reverse().ToArray());
        }

        //
        // Returns the largest memory region in bytes available for use by UFP
        //
        public ulong? ReadLargestMemoryRegion()
        {
            byte[] Bytes = ReadParam("LGMR");
            return Bytes == null || Bytes.Length != 8 ? null : BitConverter.ToUInt64(Bytes.Reverse().ToArray());
        }

        public ulong? ReadLogSize(DeviceLogType LogType)
        {
            byte[] Request = new byte[0x10];
            string Header = ReadParamSignature; // NOKXFR
            const string Param = "LZ\0\0";

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Param), 0, Request, 7, Param.Length);

            Request[15] = (byte)LogType;

            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 0x10))
            {
                return 0;
            }

            byte[] Result = new byte[Response[0x10]];
            Buffer.BlockCopy(Response, 0x11, Result, 0, Response[0x10]);

            return BitConverter.ToUInt64(Result.Reverse().ToArray(), 0);
        }

        //
        // Reads the MAC Address in the following format: "%02x-%02x-%02x-%02x-%02x-%02x"
        //
        public string ReadMacAddress()
        {
            return ReadStringParam("MAC\0");
        }

        public uint? ReadModeData(Mode Mode)
        {
            byte[] Request = new byte[0x10];
            string Header = ReadParamSignature; // NOKXFR
            string Param = "MODE";

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Param), 0, Request, 7, Param.Length);

            Request[15] = (byte)Mode;

            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 0x10))
            {
                return null;
            }

            byte[] Result = new byte[Response[0x10]];
            Buffer.BlockCopy(Response, 0x11, Result, 0, Response[0x10]);
            return Result == null || Result.Length != 4 ? null : BitConverter.ToUInt32(Result.Reverse().ToArray());
        }

        public string ReadProcessorManufacturer()
        {
            return ReadStringParam("pm\0\0");
        }

        //
        // Gets the SD Card Size in sectors, if present
        //
        public uint? ReadSDCardSize()
        {
            byte[] Bytes = ReadParam("SDS\0");
            return Bytes == null || Bytes.Length != 4 ? null : BitConverter.ToUInt32(Bytes.Reverse().ToArray());
        }

        public string ReadSupportedFFUProtocolInfo()
        {
            // TODO
            return ReadStringParam("SFPI");
        }

        public string ReadSMBIOSData()
        {
            // TODO
            return ReadStringParam("SMBD");
        }

        public Guid? ReadSerialNumber()
        {
            byte[] Bytes = ReadParam("SN\0\0");
            return Bytes == null || Bytes.Length != 16 ? null : new Guid(Bytes);
        }

        //
        // Returns the size of system memory in kB
        //
        public ulong? ReadSizeOfSystemMemory()
        {
            byte[] Bytes = ReadParam("SOSM");
            return Bytes == null || Bytes.Length != 8 ? null : BitConverter.ToUInt64(Bytes.Reverse().ToArray());
        }

        public string ReadSecurityStatus()
        {
            // TODO
            return ReadStringParam("SS\0\0");
        }

        public string ReadTelemetryLogSize()
        {
            // TODO
            return ReadStringParam("TELS");
        }

        public uint? ReadTransferSize()
        {
            byte[] Bytes = ReadParam("TS\0\0");
            return Bytes == null || Bytes.Length != 4 ? null : BitConverter.ToUInt32(Bytes.Reverse().ToArray());
        }

        //
        // Reads the UEFI Boot Flag variable content and returns it as a string.
        //
        public string ReadUEFIBootFlag()
        {
            return ReadStringParam("UBF\0");
        }

        public string ReadUEFIBootOptions()
        {
            // TODO
            return ReadStringParam("UEBO");
        }

        //
        // Reads the device properties from the UEFI Variable "UnlockID"
        // in the g_guidOfflineDUIdEfiNamespace namespace and returns it as a string.
        //
        public string ReadUnlockID()
        {
            return ReadStringParam("UKID");
        }

        public string ReadUnlockTokenFiles()
        {
            // TODO
            return ReadStringParam("UKTF");
        }

        public USBSpeed? ReadUSBSpeed()
        {
            byte[] Bytes = ReadParam("USBS");
            return Bytes == null || Bytes.Length != 2
                ? null
                : new USBSpeed()
                {
                    CurrentUSBSpeed = Bytes[0],
                    MaxUSBSpeed = Bytes[1]
                };
        }

        public uint? ReadWriteBufferSize()
        {
            byte[] Bytes = ReadParam("WBS\0");
            return Bytes == null || Bytes.Length != 4 ? null : BitConverter.ToUInt32(Bytes.Reverse().ToArray());
        }

        public void Relock()
        {
            byte[] Request = new byte[7];
            string Header = RelockSignature; // NOKXFO
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
        }

        public void MassStorage()
        {
            byte[] Request = new byte[7];
            string Header = MassStorageSignature; // NOKM
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
        }

        public void RebootPhone()
        {
            byte[] Request = new byte[7];
            string Header = $"{SwitchModeSignature}R"; // NOKXCBR
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
        }

        public void SwitchToUFP()
        {
            byte[] Request = new byte[7];
            string Header = $"{SwitchModeSignature}U"; // NOKXCBU
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
        }

        public void ContinueBoot()
        {
            byte[] Request = new byte[7];
            string Header = $"{SwitchModeSignature}W"; // NOKXCBW
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
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

            _ = ExecuteRawMethod(Request);
        }

        public void ClearScreen()
        {
            byte[] Request = new byte[6];
            string Header = ClearScreenSignature; // NOKXCC
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
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

        // WIP!
        public string ReadLog()
        {
            byte[] Request = new byte[0x13];
            string Header = GetLogsSignature;
            ulong BufferSize = 0xF000 - 0xC;

            ulong Length = ReadLogSize(DeviceLogType.Flashing)!.Value;
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