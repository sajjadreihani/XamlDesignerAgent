namespace XamlDesignerAgent.AI.Models.Planner;

public class AiPlanResponse
{
    public string Goal { get; set; } = string.Empty;
    public List<PlanStep> Steps { get; set; } = [];
}

public class PlanStep
{
    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ExpectedOutcome { get; set; } = string.Empty;
}
