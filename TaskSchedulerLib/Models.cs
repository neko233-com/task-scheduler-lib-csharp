using System.Text.Json.Serialization;

namespace TaskSchedulerLib;

// Wire models — JSON field names match server-task-scheduler exactly.
// See server-task-scheduler/ARCHITECTURE.md.

public class RegisterRequest
{
    public string name { get; set; } = "";
    public string address { get; set; } = "";
    public string app { get; set; } = "";
    public List<string> tasks { get; set; } = new();
}

public class RegisterResponse
{
    public string worker_id { get; set; } = "";
    public int ttl_seconds { get; set; }
}

public class HeartbeatRequest
{
    public string worker_id { get; set; } = "";
    public List<string> tasks { get; set; } = new();
}

public class GenericAck
{
    public bool ok { get; set; }
    public int ttl_seconds { get; set; }
}

public class DeregisterRequest
{
    public string worker_id { get; set; } = "";
}

public class ExecuteRequest
{
    public string job_id { get; set; } = "";
    public string task_key { get; set; } = "";

    [JsonPropertyName("params")]
    public Dictionary<string, object?>? @params { get; set; }

    public int attempt { get; set; }
    public int timeout_seconds { get; set; }
}

public class ExecuteResponse
{
    public bool success { get; set; }
    public string output { get; set; } = "";
    public string error { get; set; } = "";
}
