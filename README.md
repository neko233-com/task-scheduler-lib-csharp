# task-scheduler-lib-csharp

C# / .NET SDK for [server-task-scheduler](https://github.com/neko233-com/server-task-scheduler).
An embeddable worker that **auto-registers** with the center, sends heartbeats,
declares the tasks it can run, and serves `POST /execute` (ASP.NET Core) so the
center can dispatch scheduled jobs to it.

**Vendored by design** — copy the `TaskSchedulerLib` folder into your solution,
or use it as a git submodule. No NuGet publish required.

## Requirements

- .NET 10 SDK (the library targets `net10.0`). Adjust `TargetFramework` if you
  must support an older runtime.

## Use

Reference `TaskSchedulerLib` from your project, then:

```csharp
using TaskSchedulerLib;

var worker = new Worker(
    serverUrl:  "http://localhost:6432", // center
    name:       "my-worker",
    app:        "my-app",
    listenUrl:  "http://localhost:8083", // local bind
    publicUrl:  "http://localhost:8083"  // public callback URL
);

worker.Handle("send_email", async (parameters) =>
{
    var to = parameters.TryGetValue("to", out var v) && v is JsonElement je
        ? je.GetString() : "";
    return $"queued email to {to}";
});

await worker.RunAsync(cancellationToken); // registers, heartbeats, serves /execute
```

## API

| Member | Description |
|--------|-------------|
| `new Worker(serverUrl, name, app, listenUrl, publicUrl)` | Construct a worker. |
| `Handle(taskKey, fn)` | Register a handler `fn(Dictionary<string,object?>) -> Task<string>`. Exceptions become a failed task. |
| `RunAsync(token)` | Register, start heartbeat, serve `/execute`, deregister on cancellation. |

The center calls `POST {publicUrl}/execute` with
`{job_id, task_key, params, attempt, timeout_seconds}` and expects
`{success, output, error}`.

> `params` arrives as a JSON object. Values are deserialized as `JsonElement`;
> use `je.GetString()` / `je.GetInt32()` etc. to read them.

## Run the demo

```bash
SCHEDULER_URL=http://localhost:6432 dotnet run --project Demo
# then on the center:
curl -X POST http://localhost:6432/api/v1/schedules -H 'Content-Type: application/json' \
  -d '{"name":"demo","task_key":"hello","cron":"*/30 * * * *","params":{"name":"world"},"enabled":true}'
```
