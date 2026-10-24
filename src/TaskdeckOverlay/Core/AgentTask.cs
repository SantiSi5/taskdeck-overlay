namespace TaskdeckOverlay.Core;

public enum AgentTaskStatus
{
    Running,
    Waiting,
    Done,
    Error
}

/// <summary>One unit of agent work (a turn in a Claude Code session).</summary>
public sealed class AgentTask
{
    public string Name { get; set; } = "session";
    public AgentTaskStatus Status { get; set; } = AgentTaskStatus.Running;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>Sub-tasks completed / created, when the agent reports them.</summary>
    public int Completed { get; set; }
    public int Total { get; set; }
}

/// <summary>Immutable copy of a task for the UI thread.</summary>
public sealed record AgentTaskSnapshot(
    string Name,
    AgentTaskStatus Status,
    TimeSpan Elapsed,
    int Completed,
    int Total);
