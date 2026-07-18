using System.Diagnostics;
using System.Text;
using XamlDesignerAgent.AI.Interfaces;
using XamlDesignerAgent.AI.Models;
using XamlDesignerAgent.AI.Models.Planner;

namespace XamlDesignerAgent.AI.Services;

public class AgentsOrchestrator(IPlannerAgent planner, IDesignerAgent designer, IReviewAgent reviewer, ILogger<AgentsOrchestrator> logger, IAgentLog logs) : IAgentsOrchestrator
{
    public async Task<PipelineResult> RunAsync(AgentInput input, int maxSteps = 5)
    {
        var result = new PipelineResult(default, input.Code, input.Propmt, default);

        try
        {
            logger.LogInformation(
                "Starting pipeline. Request: {Request}",
                input.Propmt);

            logs.Clear();
            logs.Log("Orchestrator", "info", "Pipeline started");

            var report = new StepReport { Step = 1 };

            logs.Log("Orchestrator", "info", "▶ Running Planner...");
            var plan = await planner.CreatePlan(input.Propmt, input.Code);
            report.Plan = plan;
            logger.LogInformation(plan);

            logs.Log("Orchestrator", "info", "▶ Running Designer...");
            var xamlCode = await designer.GenerateXAML(plan, input.Code);
            report.Design = xamlCode;

            result = result with
            {
                OriginalRequest = input.Code,
                FinalCode = xamlCode
            };

            logger.LogInformation(
                "XAML Code : {XamlCode}",
                xamlCode
                );

            logs.Log("Orchestrator", "info", "▶ Running Verifier...");
            var verification = await reviewer.VerifyXAML(xamlCode);
            report.Verification = verification;

            logger.LogInformation("Verification: {Verdict}", verification.Valid);

            result.Reports.Add(report);

            var step = 1;

            while (!verification.Valid && verification.Issues?.Any() == true && step <= maxSteps)
            {
                logs.Log("Orchestrator", "warning",
                    $"▶ Step {step}/{maxSteps}: Fixing {verification.Issues.Count} issue(s)...");

                // Log each issue being fixed
                foreach (var issue in verification.Issues.Where(i => i.Severity == Severity.Error))
                    logs.Log("Orchestrator", "info", $"  Fixing: {issue.Location} — {issue.Description}");

                step++;

                verification = await reviewer.VerifyXAML(
                    verification.CorrectedXaml,
                    previousIssues: verification.Issues); 

                report.Verification = verification;

                result = result with { FinalCode = verification.CorrectedXaml };

                if (verification.Valid)
                    logs.Log("Orchestrator", "success", $"✅ All issues resolved after {step - 1} fix(es)");
                else
                    logs.Log("Orchestrator", "warning",
                        $"Still {verification.Issues?.Count ?? 0} issue(s) remaining after step {step - 1}");
            }


            if (verification.Valid)
            {
                logger.LogInformation("Pipeline complete after {Step} step(s).", step);
                logs.Log("Orchestrator", "success", "✅ Pipeline complete");
                return result with
                {
                    Success = true,
                    FinalCode = verification.CorrectedXaml,
                    TotalIterations = step
                };
            }

            logger.LogWarning(
                "Remaining issues after step {Step}: {Issues}",
                step, string.Join(", ", verification.Issues.Select(i => i.Description)));
            logs.Log("Orchestrator", "warning", $"⚠️ Pipeline completed with issues: {string.Join(", ", verification.Issues.Select(i => i.Description))}");


            logger.LogWarning("Max steps ({MaxSteps}) reached without full completion.", maxSteps);
            logs.Log("Orchestrator", "warning", "⚠️ Max steps reached. Partial improvements may have been applied.");
            return result with
            {
                FinalCode = verification.CorrectedXaml,
                FinalVerdict = "Max steps reached. Partial improvements may have been applied.",
                TotalIterations = maxSteps
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            logs.Log("Orchestrator", "error", $"❌ Pipeline failed: {e.Message}");
            return new PipelineResult(false, result.FinalCode, e.Message, 0);
        }

    }
}
