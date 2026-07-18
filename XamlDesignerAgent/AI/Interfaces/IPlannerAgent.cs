using XamlDesignerAgent.AI.Models.Planner;

namespace XamlDesignerAgent.AI.Interfaces;

public interface IPlannerAgent
{
    Task<string> CreatePlan(string userRequest, string currentCode, string? model = null);
}
