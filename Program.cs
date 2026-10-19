using System;
using System.Runtime.InteropServices;


namespace TrashTruck
{

    class Program
    {
        private static string AppVersion => typeof(Program).Assembly.GetName().Version!.ToString();

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RecycleBinInfo64
        {
            public uint Size;
            public long Bytes;
            public long Items;
        }

        // shellapi.h uses one-byte packing on 32-bit Windows only.
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct RecycleBinInfo32
        {
            public uint Size;
            public long Bytes;
            public long Items;
        }

        [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int QueryRecycleBin(string rootPath, ref RecycleBinInfo64 info);

        [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int QueryRecycleBin(string rootPath, ref RecycleBinInfo32 info);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        static int Main(string[] args)
        {
            return Run(args, rootPath => SHEmptyRecycleBin(IntPtr.Zero, rootPath,
                SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND));
        }

        internal static int Run(string[] args, Func<string?, uint> emptyRecycleBin,
            Func<string, (int Result, long Bytes, long Items)>? queryRecycleBin = null,
            Func<string[]>? getDriveRoots = null)
        {
            bool quiet = Array.Exists(args, arg => arg == "--quiet");
            bool help = Array.Exists(args, arg => arg == "--help" || arg == "-h");
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            try
            {
                if (quiet && !help)
                {
                    Console.SetOut(TextWriter.Null);
                    Console.SetError(TextWriter.Null);
                }

                return RunCore(args, emptyRecycleBin, queryRecycleBin, getDriveRoots, quiet);
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }

        private static int RunCore(string[] args, Func<string?, uint> emptyRecycleBin,
            Func<string, (int Result, long Bytes, long Items)>? queryRecycleBin,
            Func<string[]>? getDriveRoots, bool quiet)
        {
            if (Array.Exists(args, arg => arg == "--help" || arg == "-h"))
            {
                Console.WriteLine($"""
                    TrashTruck {AppVersion} - Empty the current user's Recycle Bin on selected or all drives.

                    Usage:
                      TrashTruck [--list | --all | /y | --version | --help | -h] [--quiet]
                      TrashTruck <drive> [/y] [--quiet]

                    Options:
                      --list      List Recycle Bin item counts and used space per drive,
                                  plus a total for successfully queried drives. Deletes nothing.
                      <drive>     Clean only this drive. Accepts c, c:, or C:\ (case-insensitive).
                                  Asks for y/n confirmation unless /y or --quiet is supplied.
                      --all       Empty the Recycle Bin on all drives without confirmation.
                      /y          Skip confirmation. Without a drive, targets all drives.
                      --quiet     Suppress all output, including errors and summaries, and
                                  assume yes to all confirmations for the selected operation.
                                  Without a drive or --list, empties all drives without asking.
                                  Exit code: 0 = success, 1 = failure. Help is always displayed.
                      --help, -h  Show this help message and exit without deleting anything.
                      --version   Show the app version and exit without deleting anything.

                    With no arguments, shows app information and waits for y/n confirmation
                    before emptying the current user's Recycle Bin on all drives.
                    --all, /y, and --quiet skip confirmation. Deletion is permanent, with no progress
                    dialogs or sound.
                    --list cannot be combined with a drive, --all, or /y.
                    --all cannot be combined with a drive.
                    After deletion, shows the drives processed and the estimated number and
                    size of items removed, based on before/after Recycle Bin measurements.

                    Examples:
                      TrashTruck c:
                      TrashTruck C:\ /y
                      TrashTruck C:\ --quiet
                    """);
                return 0;
            }

            if (Array.Exists(args, arg => arg == "--version"))
            {
                Console.WriteLine($"TrashTruck {AppVersion}");
                return 0;
            }

            string? rootPath = null;
            foreach (string arg in args)
            {
                if (arg is "--all" or "/y" or "--list" or "--quiet")
                    continue;

                string? drive = NormalizeDrive(arg);
                if (drive is null || rootPath is not null)
                {
                    Console.Error.WriteLine("Unknown argument or multiple drives. Use --help for usage information. No files deleted.");
                    return 1;
                }

                rootPath = drive;
            }

            if (Array.Exists(args, arg => arg == "--list"))
            {
                if (rootPath is not null || Array.Exists(args, arg => arg == "--all" || arg == "/y"))
                {
                    Console.Error.WriteLine("--list cannot be combined with a drive, --all, or /y. No files deleted.");
                    return 1;
                }

                return ListRecycleBins();
            }

            bool all = Array.Exists(args, arg => arg == "--all");
            if (rootPath is not null && all)
            {
                Console.Error.WriteLine("--all cannot be combined with a drive. No files deleted.");
                return 1;
            }

            string target = rootPath is null ? "all drives" : $"drive {rootPath}";
            if (!quiet && !all && !Array.Exists(args, arg => arg == "/y"))
            {
                Console.WriteLine($"TrashTruck {AppVersion} - Empty the current user's Recycle Bin.");
                Console.WriteLine($"This permanently deletes recycled files on {target}.");

                while (true)
                {
                    Console.Write($"Empty the Recycle Bin on {target}? (y/n): ");
                    string? answer = Console.ReadLine()?.Trim();

                    if (string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (answer is null || string.Equals(answer, "n", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Cancelled. No files deleted.");
                        return 0;
                    }

                    Console.WriteLine("Please enter y or n.");
                }
            }

            if (quiet)
                return emptyRecycleBin(rootPath) == 0 ? 0 : 1;

            queryRecycleBin ??= GetRecycleBinInfo;
            getDriveRoots ??= GetLocalDriveRoots;
            string[] roots = rootPath is null ? getDriveRoots() : [rootPath];
            var before = roots.ToDictionary(root => root, root => TryQueryRecycleBin(root, queryRecycleBin));

            uint result = emptyRecycleBin(rootPath);
            if (result != 0)
            {
                Console.Error.WriteLine($"Could not empty the Recycle Bin on {target} (0x{result:X8}).");
                PrintDeletionSummary(before, queryRecycleBin);
                return 1;
            }

            Console.WriteLine($"Recycle Bin(s) emptied on {target}.");
            PrintDeletionSummary(before, queryRecycleBin);
            return 0;
        }

        private static string[] GetLocalDriveRoots()
        {
            return DriveInfo.GetDrives()
                .Where(drive => drive.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Ram)
                .Select(drive => drive.Name).OrderBy(root => root).ToArray();
        }

        private static (int Result, long Bytes, long Items) TryQueryRecycleBin(string root,
            Func<string, (int Result, long Bytes, long Items)> query)
        {
            try
            {
                return query(root);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                return (ex.HResult, 0, 0);
            }
        }

        private static void PrintDeletionSummary(
            Dictionary<string, (int Result, long Bytes, long Items)> before,
            Func<string, (int Result, long Bytes, long Items)> query)
        {
            long removedItems = 0;
            long removedBytes = 0;
            int measured = 0;
            Console.WriteLine("Operation summary:");
            foreach (var (root, previous) in before)
            {
                var current = TryQueryRecycleBin(root, query);
                if (previous.Result != 0 || current.Result != 0)
                {
                    int error = previous.Result != 0 ? previous.Result : current.Result;
                    Console.WriteLine($"  {root} Summary unavailable (query failed: 0x{error:X8}); excluded from totals.");
                    continue;
                }

                long items = Math.Max(0, previous.Items - current.Items);
                long bytes = Math.Max(0, previous.Bytes - current.Bytes);
                removedItems += items;
                removedBytes += bytes;
                measured++;
                Console.WriteLine($"  {root} Removed {items:N0} items ({FormatSize(bytes)}); {current.Items:N0} items remaining ({FormatSize(current.Bytes)}).");
            }

            if (measured == 0)
            {
                Console.WriteLine("Removed item count and size unavailable; no drives could be measured.");
                return;
            }

            Console.WriteLine($"Total removed: {removedItems:N0} items, {FormatSize(removedBytes)} ({removedBytes:N0} bytes) across {measured} measured drive(s).");
            Console.WriteLine("Counts include files and folders. Totals are estimated from before/after measurements.");
        }

        private static string? NormalizeDrive(string arg)
        {
            if (arg.Length is < 1 or > 3 ||
                char.ToUpperInvariant(arg[0]) is < 'A' or > 'Z' ||
                (arg.Length >= 2 && arg[1] != ':') ||
                (arg.Length == 3 && arg[2] != '\\'))
                return null;

            return $"{char.ToUpperInvariant(arg[0])}:\\";
        }

        private static int ListRecycleBins()
        {
            Console.WriteLine($"TrashTruck {AppVersion} - Recycle Bins for the current user");
            Console.WriteLine($"{"Drive",-8} {"Type",-12} {"Items",12} {"Used space",16}  Status");

            long totalBytes = 0;
            long totalItems = 0;
            int queried = 0;
            int skipped = 0;

            foreach (DriveInfo drive in DriveInfo.GetDrives().OrderBy(drive => drive.Name))
            {
                try
                {
                    string? status = !drive.IsReady ? "Not ready" :
                        drive.DriveType is DriveType.Network or DriveType.CDRom or DriveType.NoRootDirectory
                            ? "Recycle Bin query not supported" : null;

                    if (status is not null)
                    {
                        Console.WriteLine($"{drive.Name,-8} {drive.DriveType,-12} {"-",12} {"-",16}  {status}");
                        skipped++;
                        continue;
                    }

                    var (result, bytes, items) = GetRecycleBinInfo(drive.Name);
                    if (result != 0)
                    {
                        Console.WriteLine($"{drive.Name,-8} {drive.DriveType,-12} {"-",12} {"-",16}  Query failed (0x{result:X8})");
                        skipped++;
                        continue;
                    }

                    Console.WriteLine($"{drive.Name,-8} {drive.DriveType,-12} {items,12:N0} {FormatSize(bytes),16}  {(items == 0 ? "Empty" : "Contains items")}");
                    totalBytes += bytes;
                    totalItems += items;
                    queried++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    Console.WriteLine($"{drive.Name,-8} {"-",-12} {"-",12} {"-",16}  Unavailable: {ex.Message}");
                    skipped++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Total used space: {FormatSize(totalBytes)} ({totalBytes:N0} bytes)");
            Console.WriteLine($"Total items: {totalItems:N0} across {queried} successfully queried drive(s).");
            if (skipped > 0)
                Console.WriteLine($"Excluded from totals: {skipped} unavailable, unsupported, or failed drive(s).");

            return queried > 0 ? 0 : 1;
        }

        private static (int Result, long Bytes, long Items) GetRecycleBinInfo(string rootPath)
        {
            if (Environment.Is64BitProcess)
            {
                var info = new RecycleBinInfo64 { Size = (uint)Marshal.SizeOf<RecycleBinInfo64>() };
                int result = QueryRecycleBin(rootPath, ref info);
                return (result, info.Bytes, info.Items);
            }

            var info32 = new RecycleBinInfo32 { Size = (uint)Marshal.SizeOf<RecycleBinInfo32>() };
            int result32 = QueryRecycleBin(rootPath, ref info32);
            return (result32, info32.Bytes, info32.Items);
        }

        private static string FormatSize(long bytes)
        {
            string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return $"{size:N2} {units[unit]}";
        }
    }
}
