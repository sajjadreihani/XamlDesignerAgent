using System.ComponentModel;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using XamlDesignerAgent.AI.Interfaces;
using XamlDesignerAgent.Renderer.Interface;

namespace XamlDesignerAgent.AI.Tools;

public class BuilderTools(IXamlRenderer xamlRenderer, IAgentLog logs)
{
    [Description("Returns the correct XAML binding syntax for common scenarios — use this to avoid mistakes")]
    public string GetBindingSyntax(
        [Description("Scenario: 'twoway-text', 'checkbox', 'combobox-items', 'listview-items', 'datagrid-items', 'visibility', 'progressbar', 'image-source', 'tabcontrol'")]
        string scenario)
    {
        logs.Log("Builder:Tool", "tool", $"🔧 GetBindingSyntax called with scenario '{scenario}'");

        var syntax = new Dictionary<string, string>
        {
            ["twoway-text"] = """<TextBox Text="Sample value" Width="200"/>""",
            ["checkbox"] = """<CheckBox Content="Enable feature" IsChecked="True"/>""",
            ["combobox-items"] = """
                                  <ComboBox SelectedIndex="0" Width="160">
                                      <ComboBoxItem Content="Option A"/>
                                      <ComboBoxItem Content="Option B"/>
                                      <ComboBoxItem Content="Option C"/>
                                  </ComboBox>
                                  """,
            ["listview-items"] = """
                                  <ListView>
                                      <ListViewItem Content="First item"/>
                                      <ListViewItem Content="Second item"/>
                                      <ListViewItem Content="Third item"/>
                                  </ListView>
                                  """,
            ["datagrid-items"] = """
                                  <DataGrid AutoGenerateColumns="False" CanUserAddRows="False">
                                      <DataGrid.Columns>
                                          <DataGridTextColumn Header="Name"  Width="*"/>
                                          <DataGridTextColumn Header="Value" Width="120"/>
                                      </DataGrid.Columns>
                                      <DataGrid.Items>
                                          <local:DataItem/>
                                      </DataGrid.Items>
                                  </DataGrid>
                                  """,
            ["visibility"] = """<TextBlock Text="Status message" Visibility="Visible"/>""",
            ["progressbar"] = """<ProgressBar Minimum="0" Maximum="100" Value="65" Height="20"/>""",
            ["image-source"] = """<Image Width="120" Height="120" Stretch="Uniform"/>""",
            ["tabcontrol"] = """
                                  <TabControl>
                                      <TabItem Header="General">
                                          <StackPanel Margin="8"/>
                                      </TabItem>
                                      <TabItem Header="Advanced">
                                          <StackPanel Margin="8"/>
                                      </TabItem>
                                  </TabControl>
                                  """
        };

        logs.Log("Builder:Tool", "tool", $"🔧 GetBindingSyntax returning syntax for scenario '{scenario}'");

        return syntax.TryGetValue(scenario, out var r)
            ? r.Trim()
            : $"Scenario '{scenario}' not found. Available: {string.Join(", ", syntax.Keys)}";
    }

    [Description("Validates XAML syntax using the WPF renderer. Returns 'valid' or a JSON error with line number.")]
    public async Task<string> ValidateXamlSyntax(
        [Description("The complete XAML string to validate")]
        string xaml)
    {
        logs.Log("Builder:Tool", "tool", "🔧 ValidateXamlSyntax called");

        if (!await xamlRenderer.IsOnlineAsync())
        {
            logs.Log("Builder:Tool", "tool", "⚠️ Renderer offline, cannot validate XAML");
            return "Renderer offline — cannot validate. Proceed carefully.";
        }

        var result = await xamlRenderer.ValidateAsync(xaml);

        logs.Log("Builder:Tool", "tool", result.Valid
            ? "✅ XAML is valid"
            : $"❌ XAML invalid: {result.Error} at line {result.Line}, position {result.Position}");

        return result.Valid
            ? "valid"
            : JsonSerializer.Serialize(new
            {
                valid = false,
                error = result.Error,
                line = result.Line,
                position = result.Position,
                hint = GetHint(result.Error ?? "")
            });
    }

    [Description("Formats and indents XAML markup. Returns the formatted XAML string.")]
    public async Task<string> FormatXaml(
        [Description("Raw XAML to format")]
        string xaml)
    {
        logs.Log("Builder:Tool", "tool", "🔧 FormatXaml called");
        if (!await xamlRenderer.IsOnlineAsync())
        {
            logs.Log("Builder:Tool", "tool", "⚠️ Renderer offline, returning unformatted XAML");
            return xaml; // return as-is if renderer offline
        }

        var result = await xamlRenderer.FormatAsync(xaml);

        logs.Log("Builder:Tool", "tool", result.Success
            ? "✅ XAML formatted successfully"
            : $"❌ XAML formatting failed: {result.Error}");

        return result.Success ? result.FormattedXaml! : xaml;
    }

    private static string GetHint(string error) => error switch
    {
        var e when e.Contains("x:Class") => "Remove the x:Class attribute — renderer has no code-behind.",
        var e when e.Contains("StaticResource") => "StaticResource key not found — define it in <Window.Resources> or remove it.",
        var e when e.Contains("Binding") => "Remove {Binding} — renderer has no ViewModel. Use hardcoded values.",
        var e when e.Contains("xmlns") => "Unknown namespace — remove custom xmlns declarations.",
        var e when e.Contains("not recognized") => "Unknown element or property. Check control name spelling.",
        _ => "Check line/position for the specific issue."
    };
}