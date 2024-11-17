using MadWizard.WinUSBNet;
using System;

namespace UFPTool
{
    internal static class ListDevice
    {
        internal static void Execute(string[] args)
        {
            USBDeviceInfo[] details = USBDevice.GetDevices("{9E3BD5F7-9690-4FCC-8810-3E2650CD6ECC}");

            for (int i = 0; i < details.Length; i++)
            {
                Console.WriteLine($"[Device {i}]");
                Console.WriteLine($"Device Path: {details[i].DevicePath}");
                Console.WriteLine($"Can Flash: True");
                Console.WriteLine();
            }

            Console.WriteLine($"Found {details.Length:d} device(s) in total.");
        }
    }
}
