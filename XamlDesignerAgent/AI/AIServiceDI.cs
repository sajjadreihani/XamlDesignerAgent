using XamlDesignerAgent.AI.Interfaces;
using XamlDesignerAgent.AI.Services;
using XamlDesignerAgent.AI.Tools;

namespace XamlDesignerAgent.AI;

public static class AIServiceDI
{
    public static void AddAIService(this IServiceCollection services)
    {
        services.AddSingleton<IAgentLog, AgentLogService>();
        services.AddScoped<PlannerTools>();
        services.AddScoped<BuilderTools>();
        services.AddScoped<IPlannerAgent, PlannerAgent>();
        services.AddScoped<IDesignerAgent, DesignerAgent>();
        services.AddScoped<IReviewAgent, ReviewAgent>();
        services.AddScoped<IAgentsOrchestrator, AgentsOrchestrator>();
        services.AddScoped<IAgentBuilder, OpenRouterAgentBuilder>();
    }
}
