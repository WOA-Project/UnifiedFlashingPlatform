using System;
using System.Diagnostics;

namespace UnifiedFlashingPlatform
{
    public partial class UnifiedFlashingPlatformTransport
    {
        public void FlashSectors(UInt32 StartSector, byte[] Data, byte TargetDevice = 0, int Progress = 0)
        {
            // Start sector is in UInt32, so max size of eMMC is 2 TB.

            byte[] Request = new byte[Data.Length + 0x40];

            string Header = FlashSignature; // NOKF
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Request[0x05] = TargetDevice; // Target device: 0: eMMC, 1: SDIO, 2: Other ???, 3: ???
            Buffer.BlockCopy(BigEndian.GetBytes(StartSector, 4), 0, Request, 0x0B, 4); // Start sector
            Buffer.BlockCopy(BigEndian.GetBytes(Data.Length / 0x200, 4), 0, Request, 0x0F, 4); // Sector count
            Request[0x13] = (byte)Progress; // Progress (0 - 100)
            Request[0x18] = 0; // Verify needed
            Request[0x19] = 0; // Skip write

            Buffer.BlockCopy(Data, 0, Request, 0x40, Data.Length);

            ExecuteRawMethod(Request);
        }

        public void Hello()
        {
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, HelloSignature);
            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
            {
                throw new BadConnectionException();
            }

            if (ByteOperations.ReadAsciiString(Response, 0, 4) != HelloSignature)
            {
                throw new WPinternalsException("Bad response from phone!", "The phone did not answer properly to the Hello message sent.");
            }
        }

        public void ResetPhone()
        {
            Debug.WriteLine("Rebooting phone");
            try
            {
                byte[] Request = new byte[4];
                ByteOperations.WriteAsciiString(Request, 0, RebootSignature);
                ExecuteRawVoidMethod(Request);
            }
            catch
            {
                Debug.WriteLine("Sending reset-request failed");
                Debug.WriteLine("Assuming automatic reset already in progress");
            }
        }

        public GPT ReadGPT()
        {
            // If this function is used with a locked BootMgr v1, 
            // then the mode-switching should be done outside this function, 
            // because the context-switches that are used here are not supported on BootMgr v1.

            // Only works in BootLoader-mode or on unlocked bootloaders in Flash-mode!!

            /*PhoneInfo Info = ReadPhoneInfo(ExtendedInfo: false);
            FlashAppType OriginalAppType = Info.App;
            bool Switch = (Info.App != FlashAppType.BootManager) && Info.IsBootloaderSecure;
            if (Switch)
            {
                SwitchToBootManagerContext();
            }*/

            byte[] Request = new byte[0x04];
            string Header = GetGPTSignature;

            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);

            byte[] Buffer = ExecuteRawMethod(Request);
            if ((Buffer == null) || (Buffer.Length < 0x4408))
            {
                throw new InvalidOperationException("Unable to read GPT!");
            }

            UInt16 Error = (UInt16)((Buffer[6] << 8) + Buffer[7]);
            if (Error > 0)
            {
                throw new NotSupportedException("ReadGPT: Error 0x" + Error.ToString("X4"));
            }

            // Length: 0x4400 for 512 (0x200) Sector Size (from sector 0 to sector 34)
            // Length: 0x6000 for 4096 (0x1000) Sector Size (from sector 0 to sector 6)

            UInt32 ReturnedGPTBufferLength = (UInt32)Buffer.Length - 8;
            UInt32 SectorSize;
            if (Buffer.Length == 0x4408)
            {
                SectorSize = 512;
            }
            else if (Buffer.Length == 0x6008)
            {
                SectorSize = 4096;
            }
            else
            {
                throw new NotSupportedException("ReadGPT: Unsupported output size! 0x" + ReturnedGPTBufferLength.ToString("X4"));
            }

            byte[] GPTBuffer = new byte[ReturnedGPTBufferLength - SectorSize];
            System.Buffer.BlockCopy(Buffer, 8 + (Int32)SectorSize, GPTBuffer, 0, (Int32)ReturnedGPTBufferLength - (Int32)SectorSize);

            /*if (Switch)
            {
                if (OriginalAppType == FlashAppType.FlashApp)
                {
                    SwitchToFlashAppContext();
                }
                else
                {
                    SwitchToPhoneInfoAppContext();
                }
            }*/

            return new GPT(GPTBuffer, SectorSize);  // NOKT message header and MBR are ignored
        }

        public byte[] ReadPhoneInfo()
        {
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, InfoQuerySignature); // NOKV
            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU"))
            {
                throw new NotSupportedException();
            }

            return Response[5..];
        }

        public void Shutdown()
        {
            byte[] Request = new byte[4];
            string Header = ShutdownSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawVoidMethod(Request);
        }
    }
}
