using XamlDesignerAgent.AI.Interfaces;
using XamlDesignerAgent.AI.Models;

namespace XamlDesignerAgent.AI.Services;

public class AgentLogService : IAgentLog
{ 
    // Each entry has a level, agent name, and message

    private readonly List<AgentLogEntry> _history = [];
    private readonly List<Action<AgentLogEntry>> _subscribers = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<AgentLogEntry> History
    {
        get { lock (_lock) return _history.ToList(); }
    }

    public void Log(string agent, string level, string message)
    {
        var entry = new AgentLogEntry(agent, level, message, DateTime.Now);

        List<Action<AgentLogEntry>> subs;
        lock (_lock)
        {
            _history.Add(entry);
            if (_history.Count > 500) _history.RemoveAt(0); // cap history
            subs = [.. _subscribers];
        }

        foreach (var sub in subs)
        {
            try { sub(entry); }
            catch { /* subscriber disconnected */ }
        }
    }

    public void Clear()
    {
        lock (_lock) _history.Clear();
    }

    public IDisposable Subscribe(Action<AgentLogEntry> handler)
    {
        lock (_lock) _subscribers.Add(handler);
        return new Unsubscriber(() =>
        {
            lock (_lock) _subscribers.Remove(handler);
        });
    }

    private class Unsubscriber(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}