using XamlDesignerAgent.Renderer.Models;
using XamlDesignerAgent.Renderer.Services;

namespace XamlDesignerAgent.Renderer.Interface;

public interface IXamlRenderer
{
    Task<FormatResult> FormatAsync(string xaml);
    Task<bool> IsOnlineAsync();
    Task<RenderResult> RenderAsync(string xaml);
    Task<ValidationResult> ValidateAsync(string xaml);
}
