using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace AsarSharp.Utils
{
    /// <summary>
    /// Names the processes holding files open, through the Windows Restart Manager - the same API
    /// installers use for "close these programs first". A file Wand injected into a game (such as
    /// overlay_game_x64.dll) stays locked by that game after Wand itself is closed, and only the
    /// game's name tells the user what to close.
    /// </summary>
    internal static class FileLockInfo
    {
        private const int ErrorSuccess = 0;
        private const int ErrorMoreData = 234;
        private const int SessionKeyLength = 32;
        private const int MaxAppNameLength = 255;
        private const int MaxServiceNameLength = 63;
        private const int MaxAttempts = 3;

        /// <returns>A sentence naming the locking processes, or null when none could be identified.</returns>
        public static string DescribeLockers(IEnumerable<string> paths)
        {
            string[] files = paths?.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (files == null || files.Length == 0 || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return null;
            }

            List<string> lockers;
            try
            {
                lockers = FindLockers(files);
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                return null;
            }

            return lockers.Count == 0
                ? null
                : $"In use by {string.Join(", ", lockers)} - close it and try again.";
        }

        private static List<string> FindLockers(string[] files)
        {
            var result = new List<string>();
            if (RmStartSession(out uint session, 0, new StringBuilder(SessionKeyLength + 1)) != ErrorSuccess)
            {
                return result;
            }

            try
            {
                if (RmRegisterResources(session, (uint)files.Length, files, 0, null, 0, null) != ErrorSuccess)
                {
                    return result;
                }

                uint count = 0;
                RmProcessInfo[] infos = null;
                for (int attempt = 0; attempt < MaxAttempts; attempt++)
                {
                    uint rebootReasons = 0;
                    int status = RmGetList(session, out uint needed, ref count, infos, ref rebootReasons);
                    if (status == ErrorSuccess)
                    {
                        break;
                    }

                    if (status != ErrorMoreData)
                    {
                        return result;
                    }

                    // The list can grow between calls, so size to what was asked for and ask again.
                    count = needed;
                    infos = new RmProcessInfo[needed];
                }

                for (int i = 0; infos != null && i < count; i++)
                {
                    result.Add(DescribeProcess(infos[i]));
                }
            }
            finally
            {
                RmEndSession(session);
            }

            return result.Distinct().ToList();
        }

        private static string DescribeProcess(RmProcessInfo info)
        {
            int pid = info.Process.ProcessId;
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    return $"{process.ProcessName}.exe (PID {pid})";
                }
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                // Exited in the meantime: the Restart Manager's display name is all that is left.
                return $"{info.AppName} (PID {pid})";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RmUniqueProcess
        {
            public int ProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RmProcessInfo
        {
            public RmUniqueProcess Process;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxAppNameLength + 1)]
            public string AppName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxServiceNameLength + 1)]
            public string ServiceShortName;

            public int ApplicationType;
            public uint AppStatus;
            public uint TsSessionId;

            [MarshalAs(UnmanagedType.Bool)]
            public bool Restartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint sessionHandle, int sessionFlags, StringBuilder sessionKey);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint sessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint sessionHandle, uint fileCount, string[] fileNames,
            uint applicationCount, [In] RmUniqueProcess[] applications, uint serviceCount, string[] serviceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint sessionHandle, out uint procInfoNeeded, ref uint procInfo,
            [In, Out] RmProcessInfo[] affectedApps, ref uint rebootReasons);
    }
}
