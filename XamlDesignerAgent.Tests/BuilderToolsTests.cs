using Moq;
using XamlDesignerAgent.AI.Interfaces;
using XamlDesignerAgent.AI.Tools;
using XamlDesignerAgent.Renderer.Interface;
using XamlDesignerAgent.Renderer.Models;
using Xunit;

namespace XamlDesignerAgent.Tests;

public class BuilderToolsTests
{
    private static (Mock<IXamlRenderer> renderer, Mock<IAgentLog> logs, BuilderTools tools) CreateSut()
    {
        var renderer = new Mock<IXamlRenderer>();
        var logs = new Mock<IAgentLog>();
        var tools = new BuilderTools(renderer.Object, logs.Object);
        return (renderer, logs, tools);
    }

    [Fact]
    public async Task ValidateXamlSyntax_ReturnsValid_WhenRendererReportsValid()
    {
        var (renderer, _, tools) = CreateSut();

        renderer.Setup(r => r.IsOnlineAsync()).ReturnsAsync(true);
        renderer.Setup(r => r.ValidateAsync(It.IsAny<string>()))
                .ReturnsAsync(new ValidationResult(true, null, 0, 0));

        var result = await tools.ValidateXamlSyntax("<Window/>");

        Assert.Equal("valid", result);
    }

    [Fact]
    public async Task ValidateXamlSyntax_ReturnsErrorJson_WhenRendererReportsInvalid()
    {
        var (renderer, _, tools) = CreateSut();

        renderer.Setup(r => r.IsOnlineAsync()).ReturnsAsync(true);
        renderer.Setup(r => r.ValidateAsync(It.IsAny<string>()))
                .ReturnsAsync(new ValidationResult(false, "Unknown element 'Foo'", 3, 12));

        var result = await tools.ValidateXamlSyntax("<Window><Foo/></Window>");

        Assert.Contains("\"valid\":false", result.Replace(" ", ""));
        Assert.Contains("Unknown element", result);
        Assert.Contains("3", result);
    }

    [Fact]
    public async Task ValidateXamlSyntax_IncludesHint_ForBindingError()
    {
        var (renderer, _, tools) = CreateSut();

        renderer.Setup(r => r.IsOnlineAsync()).ReturnsAsync(true);
        renderer.Setup(r => r.ValidateAsync(It.IsAny<string>()))
                .ReturnsAsync(new ValidationResult(false, "The property 'Text' cannot be set on {Binding Username}", 0, 0));

        var result = await tools.ValidateXamlSyntax("<TextBox Text=\"{Binding Username}\"/>");

        Assert.Contains("renderer has no ViewModel", result);
    }

    [Fact]
    public async Task ValidateXamlSyntax_ReturnsFallbackMessage_WhenRendererOffline()
    {
        var (renderer, _, tools) = CreateSut();

        renderer.Setup(r => r.IsOnlineAsync()).ReturnsAsync(false);

        var result = await tools.ValidateXamlSyntax("<Window/>");

        Assert.Contains("offline", result, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("valid", result);
        renderer.Verify(r => r.ValidateAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FormatXaml_ReturnsFormattedResult_WhenRendererSucceeds()
    {
        var (renderer, _, tools) = CreateSut();

        renderer.Setup(r => r.IsOnlineAsync()).ReturnsAsync(true);
        renderer.Setup(r => r.FormatAsync(It.IsAny<string>()))
                .ReturnsAsync(new FormatResult(true, "<Window>\n    <TextBlock/>\n</Window>", null));

        var result = await tools.FormatXaml("<Window><TextBlock/></Window>");

        Assert.Contains("\n", result);
    }

    [Fact]
    public async Task FormatXaml_ReturnsOriginalInput_WhenRendererOffline()
    {
        var (renderer, _, tools) = CreateSut();

        renderer.Setup(r => r.IsOnlineAsync()).ReturnsAsync(false);

        var input = "<Window><TextBlock/></Window>";
        var result = await tools.FormatXaml(input);

        Assert.Equal(input, result);
        renderer.Verify(r => r.FormatAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FormatXaml_ReturnsOriginalInput_WhenRendererReportsFailure()
    {
        var (renderer, _, tools) = CreateSut();

        renderer.Setup(r => r.IsOnlineAsync()).ReturnsAsync(true);
        renderer.Setup(r => r.FormatAsync(It.IsAny<string>()))
                .ReturnsAsync(new FormatResult(false, null, "malformed xml"));

        var input = "<Window><TextBlock/></Window>";
        var result = await tools.FormatXaml(input);

        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData("twoway-text")]
    [InlineData("checkbox")]
    [InlineData("combobox-items")]
    [InlineData("listview-items")]
    [InlineData("datagrid-items")]
    [InlineData("visibility")]
    [InlineData("progressbar")]
    [InlineData("image-source")]
    [InlineData("tabcontrol")]
    public void GetBindingSyntax_ReturnsNonEmptySnippet_ForEachKnownScenario(string scenario)
    {
        var (_, _, tools) = CreateSut();

        var result = tools.GetBindingSyntax(scenario);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.DoesNotContain("not found", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetBindingSyntax_ReturnsHelpfulError_ForUnknownScenario()
    {
        var (_, _, tools) = CreateSut();

        var result = tools.GetBindingSyntax("nonexistent-scenario");

        Assert.Contains("not found", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("twoway-text", result); // lists available options
    }

    [Fact]
    public void GetBindingSyntax_ReturnsCleanSnippet_WithNoLeadingWhitespace()
    {
        var (_, _, tools) = CreateSut();

        var result = tools.GetBindingSyntax("tabcontrol");

        Assert.False(result.StartsWith(' ') || result.StartsWith('\n'));
    }
}