using Img2Ffu.Reader.Data;
using MadWizard.WinUSBNet;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnifiedFlashingPlatform;

namespace UFPTool
{
    internal static class FlashDevice
    {
        internal static void Execute(string[] args)
        {
            if (args.Contains("-?"))
            {
                PrintFlashDeviceHelp();
                Environment.Exit(0);
            }

            if (!args.ToList().Any(x => x.Equals("-Path", StringComparison.InvariantCultureIgnoreCase)))
            {
                Console.WriteLine("Insufficient number of arguments");
                Console.WriteLine();
                PrintFlashDeviceHelp();
                Environment.Exit(1);
            }

            string FFUPath = "";
            bool Reboot = false;
            string DevicePath = "";
            bool VerifyWrite = false;
            bool ForceSynchronousWrite = false;
            bool SkipPlatformIDCheck = false;
            bool SkipSignatureCheck = false;
            bool SkipHash = false;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("-Path", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("Insufficient number of arguments");
                        Console.WriteLine();
                        PrintFlashDeviceHelp();
                        Environment.Exit(1);
                    }

                    i++;
                    FFUPath = args[i];
                }
                else if (arg.Equals("-DevicePath", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("Insufficient number of arguments");
                        Console.WriteLine();
                        PrintFlashDeviceHelp();
                        Environment.Exit(1);
                    }

                    i++;
                    DevicePath = args[i];
                }
                else if (arg.Equals("-Reboot", StringComparison.InvariantCultureIgnoreCase))
                {
                    Reboot = true;
                }
                else if (arg.Equals("-VerifyWrite", StringComparison.InvariantCultureIgnoreCase))
                {
                    VerifyWrite = true;
                }
                else if (arg.Equals("-ForceSynchronousWrite", StringComparison.InvariantCultureIgnoreCase))
                {
                    ForceSynchronousWrite = true;
                }
                else
                {
                    Console.WriteLine($"Unknown argument: {arg}");
                    Console.WriteLine();
                    PrintFlashDeviceHelp();
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
                        Console.WriteLine($"  {i + 1}. {details[i].DevicePath} {details[i].DeviceDescription} ({details[i].Manufacturer})");
                    }
                    Environment.Exit(1);
                }

                DevicePath = details[0].DevicePath;
            }

            FlashFlags flashFlags = FlashFlags.Normal;

            if (VerifyWrite)
            {
                flashFlags |= FlashFlags.VerifyWrite;
            }

            if (ForceSynchronousWrite)
            {
                flashFlags |= FlashFlags.ForceSynchronousWrite;
            }

            if (SkipPlatformIDCheck)
            {
                flashFlags |= FlashFlags.SkipPlatformIDCheck;
            }

            if (SkipSignatureCheck)
            {
                flashFlags |= FlashFlags.SkipSignatureCheck;
            }

            if (SkipHash)
            {
                flashFlags |= FlashFlags.SkipHash;
            }

            using FileStream FFUStream = new(FFUPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            SignedImage signedImage = new(FFUStream);
            int chunkSize = signedImage.ChunkSize;
            ulong totalChunkCount = (ulong)FFUStream.Length / (ulong)chunkSize;

            FFUStream.Seek(0, SeekOrigin.Begin);

            using UnifiedFlashingPlatformTransport ufp = new(DevicePath);

            int previousPercentage = -1;

            long length = FFUStream.Length;

            Stopwatch stopwatch = new();
            stopwatch.Start();

            Console.WriteLine($"Flashing {DevicePath}");

            ufp.FlashFFU(FFUStream, new ProgressUpdater(totalChunkCount, (int percentage, TimeSpan? eta) =>
            {
                string NewText = null;
                if (percentage != null)
                {
                    NewText = $"\rFlash: {percentage:d}% completed...";
                }

                if (eta != null)
                {
                    if (NewText == null)
                    {
                        NewText = "";
                    }
                    else
                    {
                        NewText += " - ";
                    }

                    NewText += $"Estimated time remaining: {eta:h\\:mm\\:ss}";
                }

                if (NewText != null && previousPercentage != percentage)
                {
                    previousPercentage = percentage;
                    Console.Write(NewText);
                }
            }), ResetAfterwards: Reboot, Options: (byte)flashFlags);

            stopwatch.Stop();

            TimeSpan elapsed = stopwatch.Elapsed;
            double num = length / 1048576L / elapsed.TotalSeconds;

            Console.WriteLine();
            Console.WriteLine($"Device flashed successfully at {num:F3} MB/s in {elapsed.TotalSeconds:F3} seconds");
        }

        static void PrintFlashDeviceHelp()
        {
            Console.WriteLine("Flash a FFU to a device via USB or network");
            Console.WriteLine();
            Console.WriteLine("  UFPTool.exe FlashDevice [-Property] <[Value]> <[-Property] <[Value]>...>");
            Console.WriteLine();
            Console.WriteLine("Supported properties and their possible values:");
            Console.WriteLine();
            Console.WriteLine("  Path                    - Path to the FFU");
            Console.WriteLine("  DevicePath              - [Optional] Device path");
            Console.WriteLine("  Reboot                  - [Optional] Reboot the device after it is flashed successfully");
            Console.WriteLine("  VerifyWrite             - [Optional] Verify FFU is written successfully");
            Console.WriteLine("  ForceSynchronousWrite   - [Optional] Force synchronous write to storage");
            Console.WriteLine("  SkipPlatformIDCheck     - [Optional] Skip platform ID check between device and FFU file");
            Console.WriteLine("  SkipSignatureCheck      - [Optional] Skip signature check between device and FFU file");
            Console.WriteLine("  SkipHash                - [Optional] Skip hash check of FFU file chunks");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine();
            Console.WriteLine("  UFPTool.exe FlashDevice -Path d:\\flash.ffu");
        }
    }
}
