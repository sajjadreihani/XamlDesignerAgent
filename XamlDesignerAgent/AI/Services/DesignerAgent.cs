using Microsoft.Extensions.AI;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using XamlDesignerAgent.AI.Interfaces;
using XamlDesignerAgent.AI.Tools;

namespace XamlDesignerAgent.AI.Services;

public class DesignerAgent(IAgentBuilder agentBuilder, BuilderTools tools, IAgentLog logs, IConfiguration config, ILogger<DesignerAgent> logger) : IDesignerAgent
{
    private const string instruction = """
    You are an expert WPF XAML code generator for a live preview renderer.
    The renderer is a bare WPF XamlReader.Parse() call with no ViewModel, no code-behind,
    and no resource dictionaries loaded. Your output must work in this constrained environment.

    ## Modes

    **CREATE mode** — no existing XAML provided. Generate complete XAML from scratch.

    **UPDATE mode** — existing XAML is provided inside <existing_xaml> tags.
    - Use the existing XAML as your base
    - Apply only the changes described in the specification
    - Components with "action": "keep" must be copied exactly as-is
    - Components with "action": "add" must be inserted in the correct position
    - Components with "action": "modify" must be updated with the new properties
    - Components with "action": "remove" must be deleted
    - Output the COMPLETE updated XAML — never a partial snippet or diff

    ## Output format
    Return ONLY the XAML. No markdown fences, no explanation, no comments.
    The output must start with <Window and end with </Window>.

    ## Strict renderer constraints — never violate these

    ### Root element
    - Always use <Window> as the root element
    - Always include: xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    - Always include: xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    - Never include: x:Class, DataContext, or any other attribute referencing code
    - Set a sensible Title, Width, and Height on the Window

    ### No bindings
    - Never use {Binding ...} — the renderer has no ViewModel
    - Never use {StaticResource ...} unless the resource is defined inline in <Window.Resources>
    - Never use {DynamicResource ...}
    - Never use {x:Static ...}
    - Use hardcoded values everywhere: Text="John Smith", Content="Submit", IsChecked="True"
    - For lists (ListView, ListBox, DataGrid), add 2-3 hardcoded sample <ListViewItem> or rows

    ### No external references
    - No x:Class attribute anywhere
    - No xmlns:local, xmlns:vm, or any custom namespace
    - No xmlns:d or xmlns:mc (design-time namespaces)
    - No merged resource dictionaries
    - No Style references unless the Style is defined in <Window.Resources> in the same file

    ### No commands or events
    - Never use Command=, Click=, Loaded=, or any event handler attributes
    - Buttons and MenuItems must have no Command or event attributes

    ### Safe inline resources only
    - You MAY define simple styles inside <Window.Resources> using only built-in WPF types
    - You MAY use SolidColorBrush, LinearGradientBrush defined inline
    - You MAY use x:Key on resources defined inside <Window.Resources>
    - Keep resources minimal — only define what is actually used

    ### Layout rules
    - Grid must always have explicit RowDefinitions and ColumnDefinitions
    - Every Grid child must specify Grid.Row and Grid.Column
    - Use realistic hardcoded content that represents the UI's purpose
    - Prefer simple, clean layouts — no deeply nested structures

    ## Example of correct output structure

    <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            Title="Login" Width="400" Height="300">
        <Window.Resources>
            <Style x:Key="PrimaryButton" TargetType="Button">
                <Setter Property="Background" Value="#0078D4"/>
                <Setter Property="Foreground" Value="White"/>
                <Setter Property="Padding" Value="12,6"/>
            </Style>
        </Window.Resources>
        <Grid Margin="24">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Row="0" Grid.Column="0" Text="Username" Margin="0,0,12,8" VerticalAlignment="Center"/>
            <TextBox Grid.Row="0" Grid.Column="1" Text="john.smith" Margin="0,0,0,8"/>
            <TextBlock Grid.Row="1" Grid.Column="0" Text="Password" Margin="0,0,12,8" VerticalAlignment="Center"/>
            <PasswordBox Grid.Row="1" Grid.Column="1" Margin="0,0,0,16"/>
            <Button Grid.Row="2" Grid.Column="1" Content="Sign In" Style="{StaticResource PrimaryButton}" HorizontalAlignment="Right"/>
        </Grid>
    </Window>
    """;

    public async Task<string> GenerateXAML(string plan, string currentCode, string? model = null)
    {
        model = string.IsNullOrWhiteSpace(model)
            ? config["Models:Designer"] ?? "poolside/laguna-m.1:free"
            : model;
        logs.Log("Designer", "info", $"Starting XAML generation. Model: {model}");

        var chatOptions = new ChatOptions
        {
            Tools =
                    [
                        AIFunctionFactory.Create(tools.GetBindingSyntax),
                AIFunctionFactory.Create(tools.ValidateXamlSyntax),
                AIFunctionFactory.Create(tools.FormatXaml),
            ],
            ToolMode = ChatToolMode.Auto
        };

        var agent = agentBuilder.Build(model, instruction, chatOptions);

        var message = string.IsNullOrWhiteSpace(currentCode)
            ? plan
            : $"""
          {plan}

          <existing_xaml>
          {currentCode}
          </existing_xaml>
          """;

        logs.Log("Designer", "info", "Calling model...");

        string? responseText = null;
        try
        {
            var res = await agent.RunAsync(message);
            responseText = res.Text;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Designer model call failed for {Model}", model);

            // Try configured fallbacks
            var fallbacks = config.GetSection("Models:Fallbacks").Get<string[]>() ?? Array.Empty<string>();
            foreach (var fb in fallbacks)
            {
                try
                {
                    var fallbackAgent = agentBuilder.Build(fb, instruction, chatOptions);
                    var fres = await fallbackAgent.RunAsync(message);
                    responseText = fres.Text;
                    logs.Log("Designer", "info", $"Fallback model {fb} succeeded");
                    break;
                }
                catch (Exception ex2)
                {
                    logger.LogWarning(ex2, "Fallback model {Model} failed", fb);
                }
            }
        }

        logs.Log("Designer", "info", "Sanitizing output...");
        var xaml = Sanitize(responseText ?? currentCode ?? string.Empty);

        logs.Log("Designer", "success", $"XAML ready ({xaml.Length} chars)");
        return xaml;
    }

    private static string Sanitize(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
            return xaml;

        // Strip markdown fences if model ignored instructions
        xaml = xaml.Trim();
        if (xaml.StartsWith("```"))
        {
            xaml = Regex.Replace(xaml, @"^```[a-zA-Z]*\n?", "").TrimStart();
            xaml = Regex.Replace(xaml, @"```$", "").TrimEnd();
        }

        try
        {
            var doc = XDocument.Parse(xaml);
            var ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var xNs = "http://schemas.microsoft.com/winfx/2006/xaml";

            // 1. Ensure root is <Window>
            if (doc.Root!.Name.LocalName != "Window")
            {
                var window = new XElement(XName.Get("Window", ns),
                    new XAttribute("xmlns", ns),
                    new XAttribute(XNamespace.Xmlns + "x", xNs),
                    new XAttribute("Title", "Preview"),
                    new XAttribute("Width", "900"),
                    new XAttribute("Height", "600"),
                    doc.Root);
                doc = new XDocument(window);
            }

            var root = doc.Root!;

            // 2. Remove x:Class
            var xClass = root.Attribute(XName.Get("Class", xNs));
            xClass?.Remove();

            // 3. Remove all {Binding ...} attribute values
            foreach (var attr in doc.Descendants()
                                    .SelectMany(e => e.Attributes())
                                    .Where(a => a.Value.StartsWith("{Binding"))
                                    .ToList())
            {
                attr.Value = GetFallbackValue(attr.Name.LocalName);
            }

            // 4. Remove Command= and event handler attributes
            var dangerousAttrs = new HashSet<string>
        {
            "Command", "CommandParameter", "Click", "Loaded",
            "SelectionChanged", "TextChanged", "Checked", "Unchecked"
        };
            foreach (var attr in doc.Descendants()
                                    .SelectMany(e => e.Attributes())
                                    .Where(a => dangerousAttrs.Contains(a.Name.LocalName))
                                    .ToList())
            {
                attr.Remove();
            }

            // 5. Remove {StaticResource} refs that aren't defined in Window.Resources
            var definedKeys = doc.Descendants()
                                 .Select(e => e.Attribute(XName.Get("Key", xNs))?.Value)
                                 .Where(k => k != null)
                                 .ToHashSet();

            foreach (var attr in doc.Descendants()
                                    .SelectMany(e => e.Attributes())
                                    .Where(a => a.Value.StartsWith("{StaticResource"))
                                    .ToList())
            {
                var key = Regex.Match(attr.Value, @"\{StaticResource\s+(\w+)\}").Groups[1].Value;
                if (!definedKeys.Contains(key))
                    attr.Remove();
            }

            // 6. Remove xmlns:local, xmlns:vm, xmlns:d, xmlns:mc
            var badNamespaces = new[] { "local", "vm", "d", "mc" };
            foreach (var attr in root.Attributes()
                                     .Where(a => a.IsNamespaceDeclaration
                                              && badNamespaces.Contains(a.Name.LocalName))
                                     .ToList())
            {
                attr.Remove();
            }

            return doc.ToString();
        }
        catch
        {
            // If XML parsing fails, return as-is and let the verifier handle it
            return xaml;
        }
    }

    private static string GetFallbackValue(string propertyName) =>
        propertyName switch
        {
            "Text" => "Sample Text",
            "Content" => "Button",
            "Header" => "Header",
            "ItemsSource" => "",         // can't fake a collection, remove effectively
            "IsChecked" => "False",
            "IsEnabled" => "True",
            "Visibility" => "Visible",
            "Value" => "50",
            _ => ""
        };
}
