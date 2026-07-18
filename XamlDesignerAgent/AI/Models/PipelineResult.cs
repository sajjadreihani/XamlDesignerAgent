using XamlDesignerAgent.AI.Models.Planner;

namespace XamlDesignerAgent.AI.Models;

public record PipelineResult(bool Success, string? FinalCode, string? OriginalRequest, int TotalIterations)
{
    public List<StepReport> Reports { get; set; } = [];
    public string FinalVerdict { get; set; } = string.Empty;
}

public class StepReport
{
    public int Step { get; set; }
    public string? Plan { get; set; }
    public string? Design { get; set; }
    public AiVerifyResponse? Verification { get; set; }
}
