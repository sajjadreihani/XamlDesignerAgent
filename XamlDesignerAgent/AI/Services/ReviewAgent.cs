using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;
using XamlDesignerAgent.AI.Interfaces;
using XamlDesignerAgent.AI.Models;
using XamlDesignerAgent.AI.Tools;

namespace XamlDesignerAgent.AI.Services;

public class ReviewAgent(IAgentBuilder agentBuilder, BuilderTools tools, IAgentLog logs, IConfiguration config, ILogger<ReviewAgent> logger) : IReviewAgent
{
    private const string instruction = """
        You are a XAML validation and correction agent. Your job is to find errors in XAML
        markup and output corrected XAML.

        ## Your responsibilities
        - Verify Grid definitions: every child's Grid.Row and Grid.Column must be within
          the declared row/column count
        - Confirm namespaces are complete and correct
        - Check for common WPF mistakes:
          * Missing RowDefinitions/ColumnDefinitions in Grid
          * x:Class attribute present — must be removed
          * {Binding ...} expressions — must be replaced with hardcoded values
          * {StaticResource ...} references not defined in <Window.Resources>
          * Event handlers (Click=, Loaded=) — must be removed
          * Custom xmlns (xmlns:local, xmlns:vm, xmlns:d, xmlns:mc) — must be removed
          * Unclosed tags
          * Missing x:Key on styles in Resources
          * Root element is not <Window>
        - Use the ValidateXamlSyntax tool to confirm your corrected XAML actually parses

        ## Input format
        You will receive the XAML to verify. When re-verifying after a previous attempt,
        the known issues from the previous pass are provided inside <previous_issues> tags —
        make sure all of them are resolved in your output.

        ## Output format
        Return a JSON object only. No markdown, no extra text.

        {
          "valid": true | false,
          "issues": [
            {
              "severity": "error | warning | suggestion",
              "location": "string — element or attribute where the issue is",
              "description": "string — what is wrong",
              "fix": "string — corrected XAML snippet for this issue"
            }
          ],
          "corrected_xaml": "string — full corrected XAML"
        }

        ## Rules
        - Always output corrected_xaml even if valid=true (return original unchanged)
        - Fix ALL errors in corrected_xaml, not just describe them
        - Use ValidateXamlSyntax tool on your corrected_xaml before finalizing
        - If ValidateXamlSyntax returns errors, fix them and validate again
        - valid=true means XamlReader.Parse() succeeds with no exceptions
        - Never include {Binding}, x:Class, or event handlers in corrected_xaml
        - Root element must always be <Window>
        """;

    public async Task<AiVerifyResponse> VerifyXAML(string xamlCode, string? model = null, IEnumerable<XamlIssue>? previousIssues = null)
    {
        model = string.IsNullOrWhiteSpace(model) ? config["Models:Reviewer"] ?? "openrouter/owl-alpha" : model;
        logs.Log("Verifier", "info", $"Starting verification. Model: {model}");

        var chatOptions = new ChatOptions
        {
            Tools = [AIFunctionFactory.Create(tools.ValidateXamlSyntax)],
            ToolMode = ChatToolMode.Auto
        };

        var agent = agentBuilder.Build(model, instruction, chatOptions);

        // Build message — include previous issues if this is a re-verify pass
        var message = BuildMessage(xamlCode, previousIssues);

        logs.Log("Verifier", "info", previousIssues?.Any() == true
            ? $"Re-verifying with {previousIssues.Count()} known issues to resolve"
            : "Calling model...");

        AiVerifyResponse? response = null;
        try
        {
            var res = await agent.RunAsync<AiVerifyResponse>(message);
            response = res.Result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Verifier model call failed for {Model}", model);
            var fallbacks = config.GetSection("Models:Fallbacks").Get<string[]>() ?? Array.Empty<string>();
            foreach (var fb in fallbacks)
            {
                try
                {
                    var fallbackAgent = agentBuilder.Build(fb, instruction, chatOptions);
                    var fres = await fallbackAgent.RunAsync<AiVerifyResponse>(message);
                    response = fres.Result;
                    logs.Log("Verifier", "info", $"Fallback model {fb} succeeded");
                    break;
                }
                catch (Exception ex2)
                {
                    logger.LogWarning(ex2, "Fallback model {Model} failed", fb);
                }
            }
        }

        if (response == null)
        {
            // Return a safe default indicating the verifier failed
            var issue = new XamlIssue
            {
                Severity = Severity.Error,
                Location = "model",
                Description = "Verifier failed to get a valid response from configured models.",
                Fix = ""
            };
            response = new AiVerifyResponse
            {
                Valid = false,
                Issues = new List<XamlIssue> { issue },
                CorrectedXaml = xamlCode
            };
        }

        // Log results
        if (response.Valid)
        {
            logs.Log("Verifier", "success", "✅ XAML is valid");
        }
        else
        {
            logs.Log("Verifier", "warning", $"Found {response.Issues?.Count ?? 0} issue(s):");
            if (response.Issues != null)
            {
                foreach (var issue in response.Issues)
                {
                    var icon = issue.Severity switch
                    {
                        Severity.Error => "❌",
                        Severity.Warning => "⚠️",
                        Severity.Suggestion => "💡",
                        _ => "•"
                    };
                    logs.Log("Verifier", $"{issue?.Severity ?? Severity.Suggestion}",
                        $"{icon} [{issue.Severity}] {issue.Location}: {issue.Description}");
                }
            }
        }

        return response;
    }

    private static string BuildMessage(string xamlCode, IEnumerable<XamlIssue>? previousIssues)
    {
        if (previousIssues?.Any() != true)
            return xamlCode;

        var issuesJson = JsonSerializer.Serialize(previousIssues,
            new JsonSerializerOptions { WriteIndented = false });

        return $"""
            {xamlCode}

            <previous_issues>
            {issuesJson}
            </previous_issues>

            All issues listed above must be resolved in your corrected_xaml output.
            """;
    }
}