using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TaskdeckOverlay.Core;

namespace TaskdeckOverlay.Sources;

/// <summary>
/// Local-only HTTP receiver for Claude Code "http" hooks.
/// Listens on 127.0.0.1 only and requires a shared bearer token, so other
/// programs cannot inject fake events. Only three fields are read from each
/// event (event name, session id, working directory); the rest, including any
/// prompt text, is discarded and never stored or logged.
/// </summary>
public sealed class HookServer : IDisposable
{
    private const int MaxBodyBytes = 1024 * 1024;

    private readonly TaskTracker _tracker;
    private readonly byte[] _expectedAuth;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();

    public bool Running { get; private set; }
    public string? Error { get; private set; }

    public HookServer(TaskTracker tracker, int port, string token)
    {
        _tracker = tracker;
        _expectedAuth = Encoding.UTF8.GetBytes("Bearer " + token);
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start()
    {
        try
        {
            _listener.Start();
            Running = true;
            _ = Task.Run(() => LoopAsync(_cts.Token));
        }
        catch (HttpListenerException ex)
        {
            Error = $"port unavailable ({ex.Message})";
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (ct.IsCancellationRequested || !_listener.IsListening)
            {
                return;
            }
            catch (HttpListenerException)
            {
                continue;
            }

            try
            {
                await HandleAsync(context).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A bad request must never stop the server.
                SafeRespond(context, 400);
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var request = context.Request;

        if (request.HttpMethod != "POST")
        {
            SafeRespond(context, 405);
            return;
        }

        byte[] given = Encoding.UTF8.GetBytes(request.Headers["Authorization"] ?? string.Empty);
        if (!CryptographicOperations.FixedTimeEquals(given, _expectedAuth))
        {
            SafeRespond(context, 401);
            return;
        }

        if (request.ContentLength64 > MaxBodyBytes)
        {
            SafeRespond(context, 413);
            return;
        }

        using var doc = await JsonDocument.ParseAsync(request.InputStream).ConfigureAwait(false);
        var root = doc.RootElement;

        string? eventName = GetString(root, "hook_event_name");
        string? sessionId = GetString(root, "session_id");
        if (eventName is not null && sessionId is not null)
        {
            _tracker.Apply(eventName, sessionId, GetString(root, "cwd"), DateTime.UtcNow);
        }

        SafeRespond(context, 200, "{}");
    }

    private static string? GetString(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void SafeRespond(HttpListenerContext context, int status, string? json = null)
    {
        try
        {
            context.Response.StatusCode = status;
            if (json is not null)
            {
                byte[] body = Encoding.UTF8.GetBytes(json);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = body.Length;
                context.Response.OutputStream.Write(body, 0, body.Length);
            }
            context.Response.Close();
        }
        catch (Exception)
        {
            // Client already gone.
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _listener.Close();
        }
        catch (ObjectDisposedException)
        {
        }
        Running = false;
    }
}
