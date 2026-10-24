using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Leonardo18.DatabaseHost.Strisciate;

/// <summary>A loopback-only HTTP receiver owned by the active Cassa session, not a Windows service.</summary>
public sealed class StrisciateReceiver(int port = 5000) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private WebApplication? _application;
    private CancellationTokenSource? _receiving;
    private string? _session;
    private bool _disposed;
    private sealed class PendingCard { public string Number=""; }
    private PendingCard? _pendingCard;
    public string? Endpoint => _application?.Urls.SingleOrDefault();

    public async Task<string> StartAsync(string session, IStrisciateSink sink, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(session)) throw new InvalidOperationException("Sessione di ricezione assente.");
        ArgumentNullException.ThrowIfNull(sink);
        await _lifecycle.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session == session && Endpoint is { } endpoint) return endpoint;
            await StopCore();
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
            // stdout is reserved for the desktop's JSON protocol. Never log card data or connection strings.
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => {
                options.Listen(IPAddress.Loopback, port);
                options.Limits.MaxRequestLineSize = 4096;
                options.Limits.MaxRequestHeadersTotalSize = 8192;
                options.Limits.MaxRequestBodySize = 0;
            });
            var app = builder.Build();
            var receiving = new CancellationTokenSource();
            var requests = new SemaphoreSlim(1, 1);
            var pendingCard = new PendingCard();
            app.MapGet("/update", async (HttpContext context) => {
                context.Response.ContentType = "text/plain; charset=utf-8";
                if (!StrisciataInput.TryParse(context.Request.Query, out var input, out var error))
                { context.Response.StatusCode = 400; await context.Response.WriteAsync(error); return; }
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, receiving.Token);
                var acquired = false;
                try
                {
                    await requests.WaitAsync(linked.Token); acquired = true;
                    await sink.InsertAsync(input!, linked.Token);
                    Interlocked.Exchange(ref pendingCard.Number,input!.NumeroTessera);
                    await context.Response.WriteAsync("OK", context.RequestAborted);
                }
                catch (OperationCanceledException) when (linked.IsCancellationRequested)
                {
                    if (!context.RequestAborted.IsCancellationRequested)
                    { context.Response.StatusCode = 503; await context.Response.WriteAsync("Ricezione in chiusura."); }
                }
                catch (StrisciateConflictException ex)
                { context.Response.StatusCode = 409; await context.Response.WriteAsync(ex.Message); }
                catch (InvalidOperationException)
                { context.Response.StatusCode = 403; await context.Response.WriteAsync("Sessione di cassa non autorizzata."); }
                catch (Exception)
                { context.Response.StatusCode = 500; await context.Response.WriteAsync("Inserimento strisciata non riuscito."); }
                finally { if (acquired) requests.Release(); }
            });
            try { await app.StartAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                receiving.Cancel(); receiving.Dispose(); await app.DisposeAsync();
                throw new InvalidOperationException($"Impossibile avviare la ricezione tessere sulla porta {port}. Verifica che non sia già in uso.");
            }
            catch
            { receiving.Cancel(); receiving.Dispose(); await app.DisposeAsync(); throw; }
            _application = app; _receiving = receiving; _session = session; _pendingCard = pendingCard;
            return Endpoint ?? throw new InvalidOperationException("Indirizzo di ricezione non disponibile.");
        }
        finally { _lifecycle.Release(); }
    }

    public async Task<string> TakeImportedCardAsync(string session,CancellationToken ct=default)
    {
        await _lifecycle.WaitAsync(ct);
        try
        {
            return _session==session && _pendingCard is {} pending
                ? Interlocked.Exchange(ref pending.Number,"") : "";
        }
        finally { _lifecycle.Release(); }
    }

    public async Task StopAsync(string session, CancellationToken ct = default)
    {
        await _lifecycle.WaitAsync(ct);
        try
        {
            // Late disposal of an older ViewModel must never stop the new station's receiver.
            if (_session == session) await StopCore();
        }
        finally { _lifecycle.Release(); }
    }

    private async Task StopCore()
    {
        var app = _application; var receiving = _receiving;
        _application = null; _receiving = null; _session = null; _pendingCard = null;
        receiving?.Cancel();
        if (app is not null)
        {
            try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await app.StopAsync(timeout.Token); }
            finally { await app.DisposeAsync(); receiving?.Dispose(); }
        }
        else receiving?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync();
        try { _disposed = true; await StopCore(); }
        finally { _lifecycle.Release(); }
    }
}
