using System;

namespace UFPTool
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Console tool for UFP devices");
            Console.WriteLine("Copyright (c) The DuoWoA authors. All rights reserved");
            Console.WriteLine();

            ParseArgs(args);

            PrintHelp();
        }

        static void ParseArgs(string[] args)
        {
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "deleteuefivariable":
                        Console.WriteLine("Not yet implemented.");
                        Environment.Exit(1);
                        break;
                    case "flashdevice":
                        FlashDevice.Execute(args);
                        Environment.Exit(0);
                        break;
                    case "getdevicelog":
                        GetDeviceLog.Execute(args);
                        Environment.Exit(0);
                        break;
                    case "getuefivariable":
                        Console.WriteLine("Not yet implemented.");
                        Environment.Exit(1);
                        break;
                    case "getunlockid":
                        GetUnlockId.Execute(args);
                        Environment.Exit(0);
                        break;
                    case "listdevice":
                        ListDevice.Execute(args);
                        Environment.Exit(0);
                        break;
                    case "massstoragedevice":
                        MassStorageDevice.Execute(args);
                        Environment.Exit(0);
                        break;
                    case "queryunlocktokenfiles":
                        Console.WriteLine("Not yet implemented.");
                        Environment.Exit(1);
                        break;
                    case "rambootdevice":
                        RambootDevice.Execute(args);
                        Environment.Exit(0);
                        break;
                    case "rebootdevice":
                        RebootDevice.Execute(args);
                        Environment.Exit(0);
                        break;
                    case "relockdevice":
                        RelockDevice.Execute(args);
                        Environment.Exit(0);
                        break;
                    case "setuefivariable":
                        Console.WriteLine("Not yet implemented.");
                        Environment.Exit(1);
                        break;
                    case "shutdowndevice":
                        ShutdownDevice.Execute(args);
                        Environment.Exit(0);
                        break;
                    case "skipdevice":
                        SkipDevice.Execute(args);
                        Environment.Exit(0);
                        break;
                    case "unlockdevice":
                        Console.WriteLine("Not yet implemented.");
                        Environment.Exit(1);
                        break;
                }
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("Supported commands");
            Console.WriteLine();
            Console.WriteLine("  DeleteUefiVariable    - Deletes UEFI variable on device");
            Console.WriteLine("  FlashDevice           - Flash a FFU to a device via USB or network");
            Console.WriteLine("  GetDeviceLog          - Get device flashing log");
            Console.WriteLine("  GetUefiVariable       - Get UEFI variable from device");
            Console.WriteLine("  GetUnlockId           - Gets unlock id from device");
            Console.WriteLine("  ListDevice            - List all flashable USB devices");
            Console.WriteLine("  MassStorageDevice     - Boot device into mass storage mode");
            Console.WriteLine("  QueryUnlockTokenFiles - Queries unlock token files from device. Returns a bitmask of populate slots.");
            Console.WriteLine("  RambootDevice         - Flash a FFU onto device RAM, and subsequently boot it");
            Console.WriteLine("  RebootDevice          - Reboot device");
            Console.WriteLine("  RelockDevice          - Retail relocks the device");
            Console.WriteLine("  SetUefiVariable       - Sets UEFI variable on device");
            Console.WriteLine("  ShutdownDevice        - Shut down device connected via USB or network");
            Console.WriteLine("  SkipDevice            - Exit UFP app and return control to the caller");
            Console.WriteLine("  UnlockDevice          - Retail unlocks device");
            Console.WriteLine();
            Console.WriteLine("  ufptool.exe [Command] -?, for help on a specific command");
        }
    }
}
