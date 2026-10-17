using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
namespace AppServices.Database;

public sealed class DatabaseHostException(string message, string errorCode = "") : Exception(message)
{
#if DEBUG
    ~DatabaseHostException() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public string ErrorCode { get; } = errorCode;
}
public interface IDatabaseHostClient
{
    Task<DatabaseHostResponse> SendAsync(DatabaseHostRequest request, CancellationToken ct = default);
}
/// <summary>One managed worker per executable path, shared by all views. Credentials travel only through stdin.</summary>
public sealed class DatabaseHostClient : IDatabaseHostClient
{
#if DEBUG
    ~DatabaseHostClient() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    private static readonly ConcurrentDictionary<string, WorkerSession> Sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _hostPath;
    static DatabaseHostClient() => AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
    public DatabaseHostClient(string? hostPath = null) => _hostPath = Path.GetFullPath(hostPath
        ?? Path.Combine(AppContext.BaseDirectory, "DatabaseHost", "Leonardo18.DatabaseHost.exe"));

    public Task<DatabaseHostResponse> SendAsync(DatabaseHostRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        return Sessions.GetOrAdd(_hostPath, static path => new WorkerSession(path)).SendAsync(request, ct);
    }

    /// <summary>Called when the application exits; never tie the worker's lifetime to an individual view.</summary>
    public static void Shutdown()
    {
        foreach (var pair in Sessions)
            if (Sessions.TryRemove(pair.Key, out var session)) session.Close();
    }

    private sealed class WorkerSession(string hostPath)
    {
#if DEBUG
    ~WorkerSession() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly object _processLock = new();
        private Process? _process;
        private bool _closed;

        public async Task<DatabaseHostResponse> SendAsync(DatabaseHostRequest request, CancellationToken ct)
        {
            // Serial framing keeps requests/responses associated, including calls from different views.
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            Process? process = null;
            try
            {
                ct.ThrowIfCancellationRequested();
                process = GetProcess();
                using var cancellation = ct.Register(() => Invalidate(process));
                try
                {
                    await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request,
                        DatabaseProtocolJsonContext.Default.DatabaseHostRequest).AsMemory(), ct).ConfigureAwait(false);
                    await process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
                    string? output = await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    if (output is null)
                    {
                        Invalidate(process);
                        throw new DatabaseHostException("Il componente database si è interrotto. Ripeti l'operazione.", "host_interrupted");
                    }
                    var response = JsonSerializer.Deserialize(output, DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
                    if (response is null)
                    {
                        Invalidate(process);
                        throw new DatabaseHostException("Risposta database non valida.");
                    }
                    // A rejected login or query is a response, not a reason to restart the worker.
                    if (!response.Success)
                        throw new DatabaseHostException(response.Message, response.ErrorCode);
                    return response;
                }
                catch (Exception) when (ct.IsCancellationRequested)
                {
                    Invalidate(process);
                    throw new OperationCanceledException(ct);
                }
                catch (JsonException)
                {
                    Invalidate(process);
                    throw new DatabaseHostException("Risposta database non valida.");
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
                {
                    Invalidate(process);
                    // Never retry automatically: a write might already have committed.
                    throw new DatabaseHostException("Collegamento al componente database interrotto. Ripeti la verifica.", "host_interrupted");
                }
            }
            finally { _gate.Release(); }
        }

        private Process GetProcess()
        {
            lock (_processLock)
            {
                if (_closed) throw new DatabaseHostException("Il componente database è stato chiuso.");
                if (_process is { HasExited: false }) return _process;
                _process?.Dispose(); _process = null;
                if (!File.Exists(hostPath))
                    throw new DatabaseHostException("Componente database non disponibile. Ripristina l'installazione di Leonardo18.");
                var process = new Process { StartInfo = new ProcessStartInfo(hostPath)
                {
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
                }};
                try
                {
                    if (!process.Start()) throw new DatabaseHostException("Impossibile avviare il componente database.");
                    _process = process;
                    _ = DrainStderr(process);
                    return process;
                }
                catch { process.Dispose(); throw; }
            }
        }
        private static async Task DrainStderr(Process process)
        {
            // Consume continuously without accumulating logs or retaining credential-bearing messages.
            try
            {
#if DEBUG
                while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
                    if (line.StartsWith(global::Leonardo18.Diagnostics.GcDebugLog.Prefix, StringComparison.Ordinal))
                        global::System.Diagnostics.Debug.WriteLine(line);
#else
                await process.StandardError.BaseStream.CopyToAsync(Stream.Null).ConfigureAwait(false);
#endif
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
        }
        private void Invalidate(Process process)
        {
            lock (_processLock)
            {
                if (!ReferenceEquals(_process, process)) return;
                _process = null;
                Stop(process);
            }
        }
        public void Close()
        {
            lock (_processLock)
            {
                _closed = true;
                if (_process is { } process) { _process = null; Stop(process); }
            }
            // Pending callers still need the semaphore when they unwind.
        }
        private static void Stop(Process process)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally { process.Dispose(); }
        }
    }
}