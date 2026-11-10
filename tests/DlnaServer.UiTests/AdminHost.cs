using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace DlnaServer.UiTests
{
    /// <summary>
    /// The real Host, started as its own process from its own build output, on two free ports, with a
    /// temporary configuration file, database and media tree that are deleted again on dispose.
    /// </summary>
    /// <remarks>
    /// Out of process on purpose: an in-memory TestServer leaves the local port at 0, which the port
    /// filters read, and <c>Program.Main</c> sets the process-wide working directory. See ADR 0038.
    /// </remarks>
    internal sealed class AdminHost : IAsyncDisposable
    {
        /// <summary>
        /// The name every test-run advertisement carries over SSDP, so a television on the LAN shows
        /// what it is looking at.
        /// </summary>
        public const string FriendlyName = "ZEN DLNA Server (UI tests)";

        public const string SourceFolderName = "media";
        public const string SubfolderName = "Holiday";
        public const string SmallPicture = "small.png";
        public const string LargePicture = "large.png";
        public const string HolidayPicture = "small.jpg";

        private const int MaxBufferedLines = 200;

        private static readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan _stopTimeout = TimeSpan.FromSeconds(10);

        private readonly Process _process;
        private readonly string _root;
        private readonly Queue<string> _output = new(MaxBufferedLines);

        private AdminHost(Process process, string root, int adminPort, int mediaPort)
        {
            _process = process;
            _root = root;
            AdminUri = new Uri($"http://127.0.0.1:{adminPort}");
            MediaUri = new Uri($"http://127.0.0.1:{mediaPort}");
        }

        public Uri AdminUri { get; }

        public Uri MediaUri { get; }

        public HttpClient Http { get; } = new();

        public static async Task<AdminHost> StartAsync(CancellationToken cancellationToken = default)
        {
            var hostAssembly = HostAssemblyPath();
            var root = Directory.CreateTempSubdirectory("dlna-ui-").FullName;
            var mediaFolder = Path.Combine(root, SourceFolderName);
            CopyTestMedia(hostAssembly, mediaFolder);

            var adminPort = FreePort();
            var mediaPort = FreePort();
            var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add(hostAssembly);

            // config.json is missing on purpose: the Host writes defaults carrying the shipped file-type map,
            // where a hand-written file would carry an empty map and index nothing.
            startInfo.Environment["DLNA_CONFIG_FILE"] = Path.Combine(root, "config.json");
            startInfo.Environment["ConnectionStrings__DlnaDatabase"] =
                $"Data Source={Path.Combine(root, "dlna.sqlite")};Pooling=True;Default Timeout=30;";
            startInfo.Environment["Dlna__Server__Port"] = mediaPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
            startInfo.Environment["Dlna__Server__AdminPort"] = adminPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
            startInfo.Environment["Dlna__Server__FriendlyName"] = FriendlyName;
            startInfo.Environment["Dlna__Library__SourceFolders__0"] = mediaFolder;

            // Static web assets - the admin stylesheet - are served from a build folder only in Development.
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

            var process = new Process { StartInfo = startInfo };
            var host = new AdminHost(process, root, adminPort, mediaPort);
            process.OutputDataReceived += host.OnOutput;
            process.ErrorDataReceived += host.OnOutput;
            _ = process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await host.WaitUntilReadyAsync(cancellationToken);
            }
            catch
            {
                await host.DisposeAsync();
                throw;
            }

            return host;
        }

        /// <summary>
        /// The absolute URL of an admin page.
        /// </summary>
        public Uri Admin(string path)
        {
            return new Uri(AdminUri, path);
        }

        /// <remarks>
        /// Stopped through <c>/manage/stop</c> first, so it says SSDP byebye: each run's ports - and with them
        /// its device identity - are new, and a killed Host would linger in a television's list until its
        /// advertisement expired. Killed only when that does not end it.
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                await StopGracefullyAsync();
            }

            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            await _process.WaitForExitAsync();
            _process.Dispose();
            Http.Dispose();
            Directory.Delete(_root, recursive: true);
        }

        private static string HostAssemblyPath()
        {
            var configured = typeof(AdminHost).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(static a => a.Key == "HostAssemblyPath")
                .Value!;
            var path = Path.GetFullPath(configured);

            return File.Exists(path)
                ? path
                : throw new FileNotFoundException("The Host is not built in this configuration. Build the solution first.", path);
        }

        /// <summary>
        /// Real images the Host ships as its own icons, so the tests carry no binary and SkiaSharp makes real
        /// previews from them.
        /// </summary>
        private static void CopyTestMedia(string hostAssembly, string mediaFolder)
        {
            var icons = Path.Combine(Path.GetDirectoryName(hostAssembly)!, "Resources", "images", "icons");
            var holiday = Path.Combine(mediaFolder, SubfolderName);
            _ = Directory.CreateDirectory(holiday);
            File.Copy(Path.Combine(icons, SmallPicture), Path.Combine(mediaFolder, SmallPicture));
            File.Copy(Path.Combine(icons, LargePicture), Path.Combine(mediaFolder, LargePicture));
            File.Copy(Path.Combine(icons, HolidayPicture), Path.Combine(holiday, HolidayPicture));
        }

        // ponytail: the port is free when asked and could be taken before the Host binds it; a clash fails
        // startup loudly with the Host's own output, which is the cheaper failure.
        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }

        /// <summary>
        /// Liveness on the admin port is not enough - it answers before the migrations finish - so this also
        /// waits for readiness and for the scan to have indexed every test file.
        /// </summary>
        private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_startupTimeout);

            try
            {
                while (!await IsReadyAsync(timeout.Token))
                {
                    if (_process.HasExited)
                    {
                        throw new InvalidOperationException(
                            $"The Host exited with code {_process.ExitCode} before it was ready.{Environment.NewLine}{BufferedOutput()}");
                    }

                    await Task.Delay(_pollInterval, timeout.Token);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"The Host was not ready within {_startupTimeout}.{Environment.NewLine}{BufferedOutput()}");
            }
        }

        private async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var live = await Http.GetAsync(new Uri(AdminUri, "/admin/health"), cancellationToken);
                using var ready = await Http.GetAsync(new Uri(MediaUri, "/health/ready"), cancellationToken);
                if (!live.IsSuccessStatusCode || !ready.IsSuccessStatusCode)
                {
                    return false;
                }

                var files = await Http.GetStringAsync(new Uri(MediaUri, "/manage/file"), cancellationToken);

                return files.Contains(SmallPicture, StringComparison.Ordinal)
                    && files.Contains(LargePicture, StringComparison.Ordinal)
                    && files.Contains(HolidayPicture, StringComparison.Ordinal);
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
                return false;
            }
        }

        private async Task StopGracefullyAsync()
        {
            using var timeout = new CancellationTokenSource(_stopTimeout);

            try
            {
                using var response = await Http.PostAsync(new Uri(MediaUri, "/manage/stop"), content: null, timeout.Token);
                await _process.WaitForExitAsync(timeout.Token);
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
            {
                // Not answering, or not gone in time: DisposeAsync kills it.
            }
        }

        private void OnOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is null)
            {
                return;
            }

            lock (_output)
            {
                if (_output.Count == MaxBufferedLines)
                {
                    _ = _output.Dequeue();
                }

                _output.Enqueue(e.Data);
            }
        }

        private string BufferedOutput()
        {
            lock (_output)
            {
                return string.Join(Environment.NewLine, _output);
            }
        }
    }
}
