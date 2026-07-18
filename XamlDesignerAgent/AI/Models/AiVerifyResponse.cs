using System.Text.Json.Serialization;

namespace XamlDesignerAgent.AI.Models;

public class AiVerifyResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("issues")]
    public List<XamlIssue> Issues { get; set; } = new();

    [JsonPropertyName("corrected_xaml")]
    public string CorrectedXaml { get; set; } = string.Empty;
}

public class XamlIssue
{
    [JsonPropertyName("severity")]
    public Severity Severity { get; set; }

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("fix")]
    public string Fix { get; set; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Severity
{
    Error,
    Warning,
    Suggestion
}