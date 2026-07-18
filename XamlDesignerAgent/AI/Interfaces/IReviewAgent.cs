using XamlDesignerAgent.AI.Models;

namespace XamlDesignerAgent.AI.Interfaces;

public interface IReviewAgent
{
    Task<AiVerifyResponse> VerifyXAML(string xamlCode, string? model = null, IEnumerable<XamlIssue>? previousIssues = null);
}
