using XamlDesignerAgent.AI.Models;

namespace XamlDesignerAgent.AI.Interfaces;

public interface IAgentLog
{
    IReadOnlyList<AgentLogEntry> History { get; }
    void Clear();
    void Log(string agent, string level, string message);
    IDisposable Subscribe(Action<AgentLogEntry> handler);

}
