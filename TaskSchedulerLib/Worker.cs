using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace TaskSchedulerLib;

/// <summary>
/// Embeddable worker for server-task-scheduler. Auto-registers with the center,
/// sends heartbeats, declares the tasks it can run, and serves POST /execute so
/// the center can dispatch scheduled jobs to it.
/// </summary>
public class Worker
{
    private readonly string _serverUrl;
    private readonly string _name;
    private readonly string _app;
    private readonly string _listenUrl;
    private readonly string _publicUrl;
    private readonly HttpClient _http = new();
    private readonly Dictionary<string, Func<Dictionary<string, object?>, Task<string>>> _handlers = new();
    private string _workerId = "";
    private int _ttlSeconds = 30;

    public Worker(string serverUrl, string name, string app, string listenUrl, string publicUrl)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _name = name;
        _app = app;
        _listenUrl = listenUrl;
        _publicUrl = publicUrl;
    }

    /// <summary>Register a handler for <paramref name="taskKey"/>. Call before RunAsync.</summary>
    public void Handle(string taskKey, Func<Dictionary<string, object?>, Task<string>> handler)
        => _handlers[taskKey] = handler;

    private List<string> TaskKeys() => _handlers.Keys.ToList();

    private async Task RegisterAsync()
    {
        var req = new RegisterRequest
        {
            name = _name,
            app = _app,
            address = _publicUrl,
            tasks = TaskKeys()
        };
        var resp = await PostAsync<RegisterRequest, RegisterResponse>("/api/v1/workers/register", req);
        _workerId = resp.worker_id;
        if (resp.ttl_seconds > 0) _ttlSeconds = resp.ttl_seconds;
        Console.WriteLine($"[worker] registered id={_workerId} ttl={_ttlSeconds}s tasks=[{string.Join(",", TaskKeys())}]");
    }

    private async Task HeartbeatAsync()
    {
        var req = new HeartbeatRequest { worker_id = _workerId, tasks = TaskKeys() };
        await PostAsync<HeartbeatRequest, GenericAck>("/api/v1/workers/heartbeat", req);
    }

    private async Task DeregisterAsync()
    {
        if (string.IsNullOrEmpty(_workerId)) return;
        await PostAsync<DeregisterRequest, GenericAck>(
            "/api/v1/workers/deregister",
            new DeregisterRequest { worker_id = _workerId });
    }

    private async Task HeartbeatLoopAsync(CancellationToken token)
    {
        var period = TimeSpan.FromSeconds(Math.Max(1, _ttlSeconds / 2));
        using var timer = new PeriodicTimer(period);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                try { await HeartbeatAsync(); }
                catch (Exception ex) { Console.WriteLine($"[worker] heartbeat failed: {ex.Message}"); }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Register, start the heartbeat loop, and serve /execute until
    /// <paramref name="stoppingToken"/> is cancelled. On cancellation it
    /// deregisters from the center.
    /// </summary>
    public async Task RunAsync(CancellationToken stoppingToken = default)
    {
        await RegisterAsync();
        _ = HeartbeatLoopAsync(stoppingToken);

        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        app.Urls.Add(_listenUrl);

        app.MapPost("/execute", async (ExecuteRequest req) =>
        {
            if (!_handlers.TryGetValue(req.task_key, out var handler))
                return Results.Ok(new ExecuteResponse { success = false, error = $"unknown task_key {req.task_key}" });
            try
            {
                var output = await handler(req.@params ?? new Dictionary<string, object?>());
                return Results.Ok(new ExecuteResponse { success = true, output = output });
            }
            catch (Exception ex)
            {
                return Results.Ok(new ExecuteResponse { success = false, error = ex.Message });
            }
        });

        Console.WriteLine($"[worker] serving /execute on {_listenUrl}");
        try
        {
            await app.RunAsync(stoppingToken);
        }
        finally
        {
            await DeregisterAsync();
        }
    }

    private async Task<TOut> PostAsync<TIn, TOut>(string path, TIn body)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(body),
            System.Text.Encoding.UTF8,
            "application/json");
        var resp = await _http.PostAsync(_serverUrl + path, content);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TOut>(json)
            ?? throw new InvalidOperationException($"empty response from {path}");
    }
}
