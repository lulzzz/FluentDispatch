using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GrandCentralDispatch.Helpers
{
    internal class Helper
    {
        public static ICollection<PerformanceCounter> GetPerformanceCounters(bool enablePerformanceCounters)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && enablePerformanceCounters)
            {
                return new Collection<PerformanceCounter>
                {
                    new PerformanceCounter("Processor", "% Processor Time", "_Total"),
                    new PerformanceCounter("Processor", "% Privileged Time", "_Total"),
                    new PerformanceCounter("Processor", "% Interrupt Time", "_Total"),
                    new PerformanceCounter("Processor", "% DPC Time", "_Total"),
                    new PerformanceCounter("Memory", "Available MBytes", null),
                    new PerformanceCounter("Memory", "Committed Bytes", null),
                    new PerformanceCounter("Memory", "Commit Limit", null),
                    new PerformanceCounter("Memory", "% Committed Bytes In Use", null),
                    new PerformanceCounter("Memory", "Pool Paged Bytes", null),
                    new PerformanceCounter("Memory", "Pool Nonpaged Bytes", null),
                    new PerformanceCounter("Memory", "Cache Bytes", null),
                    new PerformanceCounter("Paging File", "% Usage", "_Total"),
                    new PerformanceCounter("PhysicalDisk", "Avg. Disk Queue Length", "_Total"),
                    new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total"),
                    new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total"),
                    new PerformanceCounter("PhysicalDisk", "Avg. Disk sec/Read", "_Total"),
                    new PerformanceCounter("PhysicalDisk", "Avg. Disk sec/Write", "_Total"),
                    new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total"),
                    new PerformanceCounter("Process", "Handle Count", "_Total"),
                    new PerformanceCounter("Process", "Thread Count", "_Total"),
                    new PerformanceCounter("System", "Context Switches/sec", null),
                    new PerformanceCounter("System", "System Calls/sec", null),
                    new PerformanceCounter("System", "Processor Queue Length", null)
                };
            }
            else
            {
                return new Collection<PerformanceCounter>();
            }
        }

        public static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        public static void ExtractResource(string embeddedFileName, string destinationPath)
        {
            var currentAssembly = Assembly.GetExecutingAssembly();
            using (var resourceToSave = currentAssembly.GetManifestResourceStream(embeddedFileName))
            {
                File.WriteAllBytes(destinationPath, ReadFully(resourceToSave));
            }
        }

        private static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }
    }
}