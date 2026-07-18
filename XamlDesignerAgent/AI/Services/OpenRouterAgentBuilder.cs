using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;
using XamlDesignerAgent.AI.Interfaces;

namespace XamlDesignerAgent.AI.Services;

public class OpenRouterAgentBuilder(IConfiguration configuration, ILogger<OpenRouterAgentBuilder> logger) : IAgentBuilder
{
    public AIAgent Build(string model, string instruction, ChatOptions options = null)
    {
        var agent = new OpenAIClient(new ApiKeyCredential(configuration["OpenRouterSettings:Key"]), new OpenAIClientOptions 
        { 
            Endpoint = new Uri(configuration["OpenRouterSettings:BaseUrl"]),
            NetworkTimeout = TimeSpan.FromMinutes(5), // free models can be very slow
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
        });

        return agent.GetChatClient(model).AsAIAgent(instruction, tools: options?.Tools).AsBuilder().Use(FunctionCallLogMiddleware).Build();
    }

    private async ValueTask<object?> FunctionCallLogMiddleware(AIAgent agent, FunctionInvocationContext context, Func<FunctionInvocationContext, CancellationToken, ValueTask<object>> next, CancellationToken cancellationToken)
    {
        logger.LogInformation($"    -- Tool : {context.Function.Name}");
        if (context.Arguments.Count > 0)
        {
            foreach (var arg in context.Arguments)
            {
                logger.LogInformation($"        --- args : {arg.Key} : {arg.Value}");
            }
        }

        return await next(context, cancellationToken);
    }
}
