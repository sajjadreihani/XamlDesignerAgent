using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace XamlDesignerAgent.AI.Interfaces;

public interface IAgentBuilder
{
    AIAgent Build(string model, string instruction, ChatOptions options = null);
}
