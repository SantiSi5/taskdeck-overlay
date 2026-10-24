namespace TaskdeckOverlay.Core;

/// <summary>
/// Keeps the state of the agent tasks, one row per Claude Code session.
/// Thread-safe: events arrive on server threads, snapshots are read by the UI.
/// Only metadata is stored (project name, status, times); never prompt content.
/// </summary>
public sealed class TaskTracker
{
    private static readonly TimeSpan KeepFinishedFor = TimeSpan.FromMinutes(10);

    private readonly object _lock = new();
    private readonly Dictionary<string, AgentTask> _tasks = new();

    /// <summary>
    /// Raised (outside the internal lock, on the calling thread) when a task starts
    /// waiting for the user, finishes, or fails. Subscribers must marshal to the UI thread.
    /// </summary>
    public event Action<string, AgentTaskStatus>? StatusChanged;

    public void Apply(string eventName, string sessionId, string? cwd, DateTime now)
    {
        (string Name, AgentTaskStatus Status)? notify = null;

        lock (_lock)
        {
            if (eventName == "SessionEnd")
            {
                _tasks.Remove(sessionId);
                return;
            }

            _tasks.TryGetValue(sessionId, out var task);

            if (eventName == "UserPromptSubmit")
            {
                // A new prompt starts a new task for this session.
                _tasks[sessionId] = new AgentTask
                {
                    Name = ProjectName(cwd, task?.Name),
                    Status = AgentTaskStatus.Running,
                    StartedAt = now
                };
                return;
            }

            if (task is null)
            {
                // The overlay was opened mid-session: nothing to finish for a stop event.
                if (eventName is "Stop" or "StopFailure")
                {
                    return;
                }

                task = new AgentTask
                {
                    Name = ProjectName(cwd, null),
                    Status = AgentTaskStatus.Running,
                    StartedAt = now
                };
                _tasks[sessionId] = task;
            }

            switch (eventName)
            {
                case "TaskCreated":
                    task.Total++;
                    break;
                case "TaskCompleted":
                    task.Completed++;
                    break;
                case "Notification":
                    // Claude is waiting for the user (permission or idle prompt).
                    if (task.Status == AgentTaskStatus.Running)
                    {
                        task.Status = AgentTaskStatus.Waiting;
                        notify = (task.Name, AgentTaskStatus.Waiting);
                    }
                    break;
                case "PostToolUse":
                    // A tool ran, so the wait is over.
                    if (task.Status == AgentTaskStatus.Waiting)
                    {
                        task.Status = AgentTaskStatus.Running;
                    }
                    break;
                case "Stop":
                    if (task.Status != AgentTaskStatus.Done)
                    {
                        task.Status = AgentTaskStatus.Done;
                        task.EndedAt ??= now;
                        notify = (task.Name, AgentTaskStatus.Done);
                    }
                    break;
                case "StopFailure":
                    if (task.Status != AgentTaskStatus.Error)
                    {
                        task.Status = AgentTaskStatus.Error;
                        task.EndedAt ??= now;
                        notify = (task.Name, AgentTaskStatus.Error);
                    }
                    break;
            }
        }

        if (notify is { } n)
        {
            StatusChanged?.Invoke(n.Name, n.Status);
        }
    }

    /// <summary>Returns up to <paramref name="max"/> tasks: active first, then the most recent finished ones.</summary>
    public IReadOnlyList<AgentTaskSnapshot> Snapshot(DateTime now, int max)
    {
        lock (_lock)
        {
            var expired = _tasks
                .Where(p => p.Value.EndedAt is { } ended && now - ended > KeepFinishedFor)
                .Select(p => p.Key)
                .ToList();
            foreach (var key in expired)
            {
                _tasks.Remove(key);
            }

            return _tasks.Values
                .OrderBy(t => t.EndedAt is null ? 0 : 1)
                .ThenByDescending(t => t.EndedAt ?? t.StartedAt)
                .Take(max)
                .Select(t => new AgentTaskSnapshot(
                    t.Name,
                    t.Status,
                    (t.EndedAt ?? now) - t.StartedAt,
                    t.Completed,
                    t.Total))
                .ToList();
        }
    }

    private static string ProjectName(string? cwd, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(cwd))
        {
            string name = System.IO.Path.GetFileName(cwd.TrimEnd('\\', '/'));
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return fallback ?? "session";
    }
}
