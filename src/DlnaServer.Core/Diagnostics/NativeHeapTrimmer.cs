using System.Runtime.InteropServices;

namespace DlnaServer.Core.Diagnostics
{
    /// <summary>
    /// Asks the C allocator to give back memory it has already freed but is still holding.
    /// </summary>
    /// <remarks>
    /// The second half of releasing memory on this server, and the half that is invisible to every
    /// managed measurement. SQLite allocates its per-connection page cache in page-sized chunks, which
    /// fall below the <c>MALLOC_MMAP_THRESHOLD_</c> the NAS deployment sets and therefore come from a
    /// glibc arena rather than from <c>mmap</c>. Closing a pooled connection frees those chunks to the
    /// arena and glibc keeps them, because only a chunk sitting at the top of an arena is returned on its
    /// own. Without this call, clearing the connection pools lowers no resident figure at all.
    /// <para>
    /// Here rather than in the host because both the management endpoint and the admin page release
    /// memory, and the admin project cannot see the host. Deliberately not an injectable service: it
    /// takes no configuration, has no state to mock, and a test that wanted to substitute it would be
    /// asserting on the allocator rather than on this server.
    /// </para>
    /// <para>
    /// Best-effort. <c>malloc_trim</c> is a GNU extension and musl - which Alpine images use - does not
    /// export it, so a missing library or entry point is an ordinary outcome on some Linux images rather
    /// than a fault worth surfacing.
    /// </para>
    /// </remarks>
    public static class NativeHeapTrimmer
    {
        /// <returns>
        /// <see langword="true"/> when the allocator reported that it released memory back to the
        /// operating system. <see langword="false"/> means either that it had nothing to release or that
        /// the call is unavailable here; the two are not distinguished, because neither is actionable and
        /// the working-set figure either side of the call is the honest answer in both cases.
        /// </returns>
        public static bool TryTrim()
        {
            if (!OperatingSystem.IsLinux())
            {
                return false;
            }

            try
            {
                return MallocTrim(0) == 1;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        // "libc.so.6", not "libc". THE SONAME IS THE ONLY NAME THAT EXISTS. A Debian-based image carries
        // /lib/x86_64-linux-gnu/libc.so.6 and no libc.so at all - the unversioned name is a linker script
        // from libc6-dev, which a runtime image has no reason to install - so DllImport("libc") threw
        // DllNotFoundException, the catch below returned false, and this never ran. Measured 2026-09-06:
        // a thumbnail run reported "settled from 177 MB to 177 MB" because of exactly this.
        //
        // Naming glibc's SONAME costs nothing on musl, which does not export malloc_trim in the first
        // place: the load fails, TryTrim answers false, and that is the correct answer there.
        //
        // DllImport rather than the source-generated LibraryImport, which needs AllowUnsafeBlocks -
        // a compiler-wide switch this solution has never turned on, for one blittable call.
        [DllImport("libc.so.6", EntryPoint = "malloc_trim")]
        private static extern int MallocTrim(nuint pad);
    }
}
