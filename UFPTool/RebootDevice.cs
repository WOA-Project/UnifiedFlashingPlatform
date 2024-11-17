using MadWizard.WinUSBNet;
using System;
using System.Linq;
using UnifiedFlashingPlatform;

namespace UFPTool
{
    internal static class RebootDevice
    {
        internal static void Execute(string[] args)
        {
            if (args.Contains("-?"))
            {
                //PrintRebootDeviceHelp();
                Environment.Exit(0);
            }

            string DevicePath = "";

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("-DevicePath", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("Insufficient number of arguments");
                        Console.WriteLine();
                        //PrintRebootDeviceHelp();
                        Environment.Exit(1);
                    }

                    i++;
                    DevicePath = args[i];
                }
                else
                {
                    Console.WriteLine($"Unknown argument: {arg}");
                    Console.WriteLine();
                    //PrintRebootDeviceHelp();
                    Environment.Exit(1);
                }
            }

            if (string.IsNullOrEmpty(DevicePath))
            {
                USBDeviceInfo[] details = USBDevice.GetDevices("{9E3BD5F7-9690-4FCC-8810-3E2650CD6ECC}");
                if (details.Length == 0)
                {
                    Console.WriteLine("No UFP devices found");
                    Environment.Exit(1);
                }

                if (details.Length > 1)
                {
                    Console.WriteLine("Multiple UFP devices found. Please specify the device path");
                    Console.WriteLine();
                    Console.WriteLine("Available devices:");
                    for (int i = 0; i < details.Length; i++)
                    {
                        Console.WriteLine($"[Device {i}]");
                        Console.WriteLine($"Device Path: {details[i].DevicePath}");
                        Console.WriteLine($"Can Flash: True");
                        Console.WriteLine();
                    }
                    Environment.Exit(1);
                }

                DevicePath = details[0].DevicePath;
            }

            using UnifiedFlashingPlatformTransport ufp = new(DevicePath);
            ufp.RebootPhone();
        }
    }
}
