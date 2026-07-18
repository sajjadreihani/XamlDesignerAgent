using XamlDesignerAgent.AI.Models;

namespace XamlDesignerAgent.AI.Interfaces;

public interface IAgentsOrchestrator
{
    Task<PipelineResult> RunAsync(AgentInput input, int maxSteps = 5);
}
