using Moq;
using Xunit;
using XamlDesignerAgent.AI.Interfaces;
using XamlDesignerAgent.AI.Models;
using XamlDesignerAgent.AI.Services;
using Microsoft.Extensions.Logging;

namespace XamlDesignerAgent.Tests;

public class AgentsOrchestratorTests
{
    private static AiVerifyResponse Valid(string xaml) =>
        new() { Valid = true, CorrectedXaml = xaml, Issues = [] };

    private static AiVerifyResponse Invalid(string xaml, params XamlIssue[] issues) =>
        new() { Valid = false, CorrectedXaml = xaml, Issues = [.. issues] };

    [Fact]
    public async Task RunAsync_StopsImmediately_WhenFirstVerificationIsValid()
    {
        var planner = new Mock<IPlannerAgent>();
        var designer = new Mock<IDesignerAgent>();
        var reviewer = new Mock<IReviewAgent>();
        var logs = new Mock<IAgentLog>();
        var logger = Mock.Of<ILogger<AgentsOrchestrator>>();

        planner.Setup(p => p.CreatePlan(It.IsAny<string>(), It.IsAny<string>()))
               .ReturnsAsync("plan-json");
        designer.Setup(d => d.GenerateXAML(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("<Window/>");
        reviewer.Setup(r => r.VerifyXAML(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<XamlIssue>?>()))
                .ReturnsAsync(Valid("<Window/>"));

        var orchestrator = new AgentsOrchestrator(
            planner.Object, designer.Object, reviewer.Object, logger, logs.Object);

        var result = await orchestrator.RunAsync(new AgentInput("Build a login form", string.Empty), maxSteps: 5);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalIterations);
        reviewer.Verify(r => r.VerifyXAML(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<XamlIssue>?>()),
            Times.Once); 
    }

    [Fact]
    public async Task RunAsync_RetriesUntilValid_AndPassesIssuesForward()
    {
        var planner = new Mock<IPlannerAgent>();
        var designer = new Mock<IDesignerAgent>();
        var reviewer = new Mock<IReviewAgent>();
        var logs = new Mock<IAgentLog>();
        var logger = Mock.Of<ILogger<AgentsOrchestrator>>();

        planner.Setup(p => p.CreatePlan(It.IsAny<string>(), It.IsAny<string>()))
               .ReturnsAsync("plan-json");
        designer.Setup(d => d.GenerateXAML(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("<Window/>");

        var issue = new XamlIssue { Severity = Severity.Error, Location = "Window", Description = "x:Class not allowed" };

        reviewer.SetupSequence(r => r.VerifyXAML(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<XamlIssue>?>()))
                .ReturnsAsync(Invalid("<Window x:Class=\"X\"/>", issue))
                .ReturnsAsync(Valid("<Window/>"));

        var orchestrator = new AgentsOrchestrator(
            planner.Object, designer.Object, reviewer.Object, logger, logs.Object);

        var result = await orchestrator.RunAsync(new AgentInput("Build a login form", string.Empty), maxSteps: 5);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalIterations);
        reviewer.Verify(r => r.VerifyXAML(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<XamlIssue>?>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_StopsAtMaxSteps_WhenNeverBecomesValid()
    {
        var planner = new Mock<IPlannerAgent>();
        var designer = new Mock<IDesignerAgent>();
        var reviewer = new Mock<IReviewAgent>();
        var logs = new Mock<IAgentLog>();
        var logger = Mock.Of<ILogger<AgentsOrchestrator>>();

        planner.Setup(p => p.CreatePlan(It.IsAny<string>(), It.IsAny<string>()))
               .ReturnsAsync("plan-json");
        designer.Setup(d => d.GenerateXAML(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("<Window/>");

        var issue = new XamlIssue { Severity = Severity.Error, Location = "Window", Description = "always broken" };

        reviewer.Setup(r => r.VerifyXAML(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<XamlIssue>?>()))
                .ReturnsAsync(Invalid("<Window/>", issue)); 

        var orchestrator = new AgentsOrchestrator(
            planner.Object, designer.Object, reviewer.Object, logger, logs.Object);

        var result = await orchestrator.RunAsync(new AgentInput("Build a login form", string.Empty), maxSteps: 3);

        Assert.False(result.Success);
        Assert.Equal(3, result.TotalIterations);
        reviewer.Verify(r => r.VerifyXAML(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<XamlIssue>?>()),
            Times.Exactly(4)); 
    }

    [Fact]
    public async Task RunAsync_ReturnsFailureResult_WhenAgentThrows()
    {
        var planner = new Mock<IPlannerAgent>();
        var designer = new Mock<IDesignerAgent>();
        var reviewer = new Mock<IReviewAgent>();
        var logs = new Mock<IAgentLog>();
        var logger = Mock.Of<ILogger<AgentsOrchestrator>>();

        planner.Setup(p => p.CreatePlan(It.IsAny<string>(), It.IsAny<string>()))
               .ThrowsAsync(new TaskCanceledException("timeout"));

        var orchestrator = new AgentsOrchestrator(
            planner.Object, designer.Object, reviewer.Object, logger, logs.Object);

        var result = await orchestrator.RunAsync(new AgentInput("Build a login form", string.Empty), maxSteps: 3);

        Assert.False(result.Success);
    }
}