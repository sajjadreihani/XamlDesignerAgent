namespace XamlDesignerAgent.AI.Interfaces;

public interface IDesignerAgent
{
    Task<string> GenerateXAML(string plan, string currentCode, string? model = null);
}
