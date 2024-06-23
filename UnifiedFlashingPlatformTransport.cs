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
        private readonly USBDevice USBDevice;
        private readonly USBPipe InputPipe;
        private readonly USBPipe OutputPipe;
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

        public byte[]? ExecuteRawMethod(byte[] RawMethod)
        {
            return ExecuteRawMethod(RawMethod, RawMethod.Length);
        }

        public byte[]? ExecuteRawMethod(byte[] RawMethod, int Length)
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

        public byte[]? Echo(byte[] DataPayload)
        {
            byte[] Request = new byte[10 + DataPayload.Length];
            string Header = EchoSignature; // NOKXCE

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(DataPayload.Length).Reverse().ToArray(), 0, Request, 6, 4);
            Buffer.BlockCopy(DataPayload, 0, Request, 10, DataPayload.Length);

            byte[]? Response = ExecuteRawMethod(Request);
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
        public string? ReadLog()
        {
            PhoneInfo Info = ReadPhoneInfo();

            byte[] Request = new byte[0x13];
            string Header = GetLogsSignature;
            ulong BufferSize = 0xE000 - 0xC;

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

                byte[]? Response = ExecuteRawMethod(Request);
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