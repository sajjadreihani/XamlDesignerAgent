using Microsoft.Extensions.AI;
using XamlDesignerAgent.AI.Interfaces;
using XamlDesignerAgent.AI.Models.Planner;
using XamlDesignerAgent.AI.Tools;
using XamlDesignerAgent.Utility;

namespace XamlDesignerAgent.AI.Services;

public class PlannerAgent(IAgentBuilder agentBuilder, PlannerTools tools, IAgentLog logs, IConfiguration config, ILogger<PlannerAgent> logger) : IPlannerAgent
{
    private const string instruction = """
    You are a senior WPF/MAUI UI Planner agent. Your job is to analyze a natural-language UI description and produce a structured UI specification that a downstream XAML code generator can use.

    ## Your responsibilities
    - Understand the user's intent: what screen, component, or layout they need
    - Identify the layout type (Grid, StackPanel, DockPanel, etc.)
    - List all UI components required (Button, TextBox, ListView, etc.)
    - Define data bindings needed (property names, collection sources)
    - Specify styles, themes, and visual requirements (colors, fonts, spacing)
    - Note interactivity: commands, events, validation rules
    - Flag any ambiguities and resolve them with sensible defaults

    ## Modes
    You operate in two modes depending on whether existing XAML is provided:

    **CREATE mode** — no existing code provided. Generate a full specification from scratch.

    **UPDATE mode** — existing XAML is provided inside <existing_xaml> tags.
    - Analyze the existing XAML structure first
    - Apply ONLY the changes described in the user prompt
    - Preserve all existing components, bindings, and styles not mentioned in the prompt
    - In the JSON output, add an "update_mode": true field
    - Add a "changes" array describing what was added, modified, or removed
    - Keep unchanged components in the "components" array as-is

    ## Output format
    Always respond with a JSON object only. No markdown, no explanation outside the JSON.

    {
      "screen_name": "string",
      "update_mode": false,
      "changes": [],
      "layout_root": "Grid | StackPanel | DockPanel | Canvas | Border",
      "layout_description": "string — describe row/column structure if Grid",
      "components": [
        {
          "id": "string",
          "type": "string — WPF/MAUI control type",
          "purpose": "string — what this control does",
          "bindings": { "property": "BindingPath" },
          "style_hints": "string — visual requirements",
          "placement": "string — where in the layout",
          "action": "keep | add | modify | remove"
        }
      ],
      "data_context": "string — expected ViewModel or BindingContext type",
      "commands": ["list of ICommand names needed"],
      "styles_required": ["list of style keys or resource keys"],
      "notes": "string — ambiguities resolved, assumptions made"
    }

    ## Rules
    - Always prefer MVVM patterns: use {Binding} for all data, never code-behind logic
    - Default to WPF unless the user specifies MAUI or UWP
    - If the user gives a vague description (e.g. "a login form"), infer all standard fields
    - Keep component IDs in PascalCase (e.g. "LoginButton", "UsernameTextBox")
    - Never generate XAML yourself — only produce the specification
    - In UPDATE mode, never drop existing components unless the prompt explicitly asks to remove them
    - Since the output targets a live renderer with no ViewModel, do NOT specify bindings or commands in the specification. The builder will use hardcoded sample values instead.
    - Do not include a "data_context" or "commands" field — leave them empty.
    """;
    
    public async Task<string> CreatePlan(string userRequest, string currentCode, string? model = null)
    {
        model = string.IsNullOrWhiteSpace(model) ? config["Models:Planner"] ?? "openrouter/owl-alpha" : model;
        logs.Log("Planner", "info", $"Starting plan for: {userRequest.Truncate(80)}");
        logs.Log("Planner", "info", $"Mode: {(string.IsNullOrWhiteSpace(currentCode) ? "CREATE" : "UPDATE")}");
        logs.Log("Planner", "info", $"Model: {model}");

        var chatOptions = new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(tools.GetComponentLibrary),
                AIFunctionFactory.Create(tools.GetLayoutSnippet),
            ],
            ToolMode = ChatToolMode.Auto
        };

        var agent = agentBuilder.Build(model, instruction, chatOptions);


        // Build the user message based on whether we have existing code
        var message = string.IsNullOrWhiteSpace(currentCode)
            ? userRequest
            : $"""
          {userRequest}

          <existing_xaml>
          {currentCode}
          </existing_xaml>
          """;

        logs.Log("Planner", "info", "Calling model...");

        string? responseText = null;
        try
        {
            var res = await agent.RunAsync(message);
            responseText = res.Text;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Planner model call failed for {Model}", model);
            var fallbacks = config.GetSection("Models:Fallbacks").Get<string[]>() ?? Array.Empty<string>();
            foreach (var fb in fallbacks)
            {
                try
                {
                    var fallbackAgent = agentBuilder.Build(fb, instruction, chatOptions);
                    var fres = await fallbackAgent.RunAsync(message);
                    responseText = fres.Text;
                    logs.Log("Planner", "info", $"Fallback model {fb} succeeded");
                    break;
                }
                catch (Exception ex2)
                {
                    logger.LogWarning(ex2, "Fallback model {Model} failed", fb);
                }
            }
        }

        logs.Log("Planner", "success", $"Plan ready ({(responseText?.Length ?? 0)} chars)");
        logs.Log("Planner", "debug", $"Plan content: {responseText}");

        return responseText ?? string.Empty;
    }
}
