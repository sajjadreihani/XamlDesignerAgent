namespace XamlDesignerAgent.Renderer.Models;

public record RenderResult(bool Success, string? ImageBase64, string? Error);
public record ValidationResult(bool Valid, string? Error, int? Line, int? Position);
public record FormatResult(bool Success, string? FormattedXaml, string? Error);
