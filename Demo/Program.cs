using System.Text.Json;
using TaskSchedulerLib;

// Demo worker for server-task-scheduler (C# SDK).
//   SCHEDULER_URL=http://localhost:6432 dotnet run --project Demo
// Then create a schedule on the center with task_key "hello".

var serverUrl = Environment.GetEnvironmentVariable("SCHEDULER_URL") ?? "http://localhost:6432";

var worker = new Worker(serverUrl, "demo-csharp", "demo", "http://localhost:8083", "http://localhost:8083");

worker.Handle("hello", async (parameters) =>
{
    var name = "world";
    if (parameters.TryGetValue("name", out var raw) && raw is JsonElement je)
    {
        name = je.GetString() ?? "world";
    }
    var msg = $"hello, {name}! (worker=demo-csharp)";
    Console.WriteLine(msg);
    return msg;
});

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await worker.RunAsync(cts.Token);
