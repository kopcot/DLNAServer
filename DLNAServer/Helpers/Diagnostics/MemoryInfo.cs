using System.Diagnostics;
using System.Runtime;
using System.Text.Json;

namespace DLNAServer.Helpers.Diagnostics
{
    public static class MemoryInfo
    {
        public static int GetObjectSize(object obj)
        {
            if (obj == null)
            {
                return 0;
            }

            try
            {
                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(obj);
                return jsonBytes.Length; // Returns size in bytes
            }
            catch
            {
                return -1;
            }
        }
        public static Dictionary<string, object> ProcessMemoryInfo()
        {
            const double fromBtoMB = 1024 * 1024;
            double allocated = GC.GetTotalMemory(forceFullCollection: false) / fromBtoMB; // Heap Size
            double totalCommittedBytes = GC.GetGCMemoryInfo().TotalCommittedBytes / fromBtoMB;
            double totalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / fromBtoMB;
            double memoryLoadBytes = GC.GetGCMemoryInfo().MemoryLoadBytes / fromBtoMB;
            double heapSizeBytes = GC.GetGCMemoryInfo().HeapSizeBytes / fromBtoMB;
            long pinnedObjectsCount = GC.GetGCMemoryInfo().PinnedObjectsCount;
            double privateMemorySize64 = 0.0;
            double workingSet64 = 0.0;
            double virtualMemorySize64 = 0.0;
            double nonpagedSystemMemorySize64 = 0.0;
            double pagedSystemMemorySize64 = 0.0;
            double pagedMemorySize64 = 0.0;
            string mainWindowTitle;
            DateTime startTime;
            int threadCount;
            Dictionary<System.Diagnostics.ThreadState, Dictionary<string, int>> threadStatusWaitReason = [];
            int pid = 0;

            using (var process = Process.GetCurrentProcess())
            {
                privateMemorySize64 = process.PrivateMemorySize64 / fromBtoMB;
                pid = process.Id;
                workingSet64 = process.WorkingSet64 / fromBtoMB;
                nonpagedSystemMemorySize64 = process.NonpagedSystemMemorySize64 / fromBtoMB;
                pagedMemorySize64 = process.PagedMemorySize64 / fromBtoMB;
                pagedSystemMemorySize64 = process.PagedSystemMemorySize64 / fromBtoMB;
                virtualMemorySize64 = process.VirtualMemorySize64 / fromBtoMB;
                mainWindowTitle = process.MainWindowTitle;
                startTime = process.StartTime;
                threadCount = process.Threads.Count;
                threadStatusWaitReason = process
                    .Threads
                    .Cast<ProcessThread>()
                    .GroupBy(static (pt) => pt.ThreadState)
                    .ToDictionary(
                        static (ptg) => ptg.Key,
                        static (ptg) => ptg.GroupBy(static (pt) => pt.ThreadState == System.Diagnostics.ThreadState.Wait
                            ? "Wait reason - " + pt.WaitReason.ToString()
                            : pt.ThreadState.ToString())
                            .ToDictionary(
                                static (wrg) => wrg.Key,
                                static (wrg) => wrg.Count()
                            )
                    );
            }
            var data = new Dictionary<string, object>
            {
                { "Main window title", mainWindowTitle },
                { "Process ID", pid },
                { "Start time", startTime },
                { "Utc time", DateTime.UtcNow },
                { "Local time", DateTime.Now },
                { "GC-collection Gen0 (count)", GC.CollectionCount(0) },
                { "GC-collection Gen1 (count)", GC.CollectionCount(1) },
                { "GC-collection Gen2 (count)", GC.CollectionCount(2) },
                { "Pinned objects count", pinnedObjectsCount },
                { "Allocated (MB)", allocated },
                { "Total committed bytes (MB)", totalCommittedBytes },
                { "Total available memory bytes (MB)", totalAvailableMemoryBytes },
                { "Memory load bytes (MB)", memoryLoadBytes },
                { "Heap size bytes (MB)", heapSizeBytes },
                { "Working set 64-bit (MB)", workingSet64 },
                { "Private memory size 64-bit (MB)", privateMemorySize64 },
                { "Virtual memory set 64-bit (MB)", virtualMemorySize64 },
                { "Paged memory size 64-bit (MB)", pagedMemorySize64 },
                { "Paged system memory size 64-bit (MB)", pagedSystemMemorySize64 },
                { "Nonpaged system memory size 64-bit (MB)", nonpagedSystemMemorySize64 },
                { "GC - Is Server GC", GCSettings.IsServerGC },
                { "GC - Large Object Heap (LOH) compaction mode", GCSettings.LargeObjectHeapCompactionMode.ToString() },
                { "GC - Latency mode ", GCSettings.LatencyMode.ToString() },
                { "GC - Is Concurrent (background) GC", GC.GetGCMemoryInfo().Concurrent },
                { "GC - Index of this GC", GC.GetGCMemoryInfo().Index },
                { "GC - Generation of this GC", GC.GetGCMemoryInfo().Generation },
                { "Threads", threadCount }
            };
            foreach (var threadState in threadStatusWaitReason)
            {
                data.Add($"Threads in state {threadState.Key}", threadState.Value);
            }

            var index = 0;
            foreach (var generationInfo in GC.GetGCMemoryInfo().GenerationInfo)
            {
                data.Add("Generation Info " + index + " - Fragmentation before bytes", generationInfo.FragmentationBeforeBytes);
                data.Add("Generation Info " + index + " - Fragmentation after bytes", generationInfo.FragmentationAfterBytes);
                data.Add("Generation Info " + index + " - Size before bytes", generationInfo.SizeBeforeBytes);
                data.Add("Generation Info " + index + " - Size after bytes", generationInfo.SizeAfterBytes);
                index++;
            }
            return data;
        }
    }
}
