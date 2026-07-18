using System.ComponentModel;
using System.Text.Json;
using XamlDesignerAgent.AI.Interfaces;

namespace XamlDesignerAgent.AI.Tools;

public class PlannerTools(IAgentLog logs)
{
    [Description("Returns a list of available WPF controls grouped by category")]
    public string GetComponentLibrary(
        [Description("Category filter: 'layout', 'input', 'display', 'list', 'action', 'container'. Leave empty for all.")]
        string? category = null)
    {
        logs.Log("Planner:Tool", "tool", $"🔧 GetComponentLibrary called with category '{category}'");

        var library = new Dictionary<string, object[]>
        {
            ["layout"] =
            [
                new { name = "Grid",         notes = "Row/column layout. Must define RowDefinitions and ColumnDefinitions." },
                new { name = "StackPanel",   notes = "Stacks children vertically or horizontally." },
                new { name = "DockPanel",    notes = "Docks children to edges. Last child fills remaining space." },
                new { name = "WrapPanel",    notes = "Wraps children to next line when space runs out." },
                new { name = "Canvas",       notes = "Absolute positioning via Canvas.Left/Top. Avoid unless needed." },
                new { name = "Border",       notes = "Adds border, background, padding around a single child." },
                new { name = "ScrollViewer", notes = "Wraps content to make it scrollable." },
            ],
            ["input"] =
            [
                new { name = "TextBox",      notes = "Single-line text input." },
                new { name = "PasswordBox",  notes = "Masked password input. No Text property — use Password." },
                new { name = "ComboBox",     notes = "Dropdown selector." },
                new { name = "CheckBox",     notes = "Boolean toggle with label." },
                new { name = "RadioButton",  notes = "Mutually exclusive option within a GroupName." },
                new { name = "Slider",       notes = "Numeric range input." },
                new { name = "DatePicker",   notes = "Calendar date selector." },
            ],
            ["display"] =
            [
                new { name = "TextBlock",    notes = "Non-editable text. Supports inline formatting." },
                new { name = "Label",        notes = "Editable target label. Use TextBlock for display-only." },
                new { name = "Image",        notes = "Displays an image via Source property." },
                new { name = "ProgressBar",  notes = "Shows progress via Value, Minimum, Maximum." },
                new { name = "Separator",    notes = "Horizontal or vertical visual divider." },
            ],
            ["list"] =
            [
                new { name = "ListBox",      notes = "Simple list of selectable items." },
                new { name = "ListView",     notes = "List with optional column headers via GridView." },
                new { name = "DataGrid",     notes = "Table with auto-generated or custom columns." },
                new { name = "TreeView",     notes = "Hierarchical list with expandable nodes." },
            ],
            ["action"] =
            [
                new { name = "Button",       notes = "Clickable button. No Command= for renderer." },
                new { name = "ToggleButton", notes = "Button that stays pressed. IsChecked property." },
                new { name = "MenuItem",     notes = "Used inside Menu or ContextMenu." },
                new { name = "Hyperlink",    notes = "Inline clickable link inside a TextBlock." },
            ],
            ["container"] =
            [
                new { name = "GroupBox",     notes = "Bordered box with a Header label." },
                new { name = "Expander",     notes = "Collapsible section with a Header." },
                new { name = "TabControl",   notes = "Tabbed container. Children must be TabItem." },
                new { name = "TabItem",      notes = "Single tab inside a TabControl. Has Header and Content." },
            ]
        };

        var result = category != null && library.TryGetValue(category, out var filtered)
            ? new Dictionary<string, object[]> { [category] = filtered }
            : library;

        logs.Log("Planner:Tool", "tool", $"🔧 GetComponentLibrary returning {(category != null ? $"filtered library for category '{category}'" : "full component library")}");

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
    }

    [Description("Returns a layout specification template for common UI screen types")]
    public string GetLayoutSnippet(
        [Description("Screen type: 'two-column-form', 'master-detail', 'dashboard', 'toolbar-content', 'wizard', 'card-list', 'header-content-footer', 'settings-page'")]
        string screenType)
    {
        logs.Log("Planner:Tool", "tool", $"🔧 GetLayoutSnippet called with screenType '{screenType}'");

        var snippets = new Dictionary<string, object>
        {
            ["two-column-form"] = new
            {
                layout_root = "Grid",
                layout_description = "Two columns: Auto for labels, * for inputs. One row per field plus a button row.",
                sample_components = new[]
                {
                    new { type = "TextBlock", placement = "Row 0, Col 0", notes = "Field label" },
                    new { type = "TextBox",   placement = "Row 0, Col 1", notes = "Field input" },
                    new { type = "Button",    placement = "Last row, Col 1, HorizontalAlignment=Right", notes = "Submit button" }
                }
            },
            ["master-detail"] = new
            {
                layout_root = "Grid",
                layout_description = "Two columns: 280px for master list, * for detail panel. Single row.",
                sample_components = new[]
                {
                    new { type = "ListBox",    placement = "Col 0", notes = "Master list of items" },
                    new { type = "ScrollViewer", placement = "Col 1", notes = "Detail content area" }
                }
            },
            ["dashboard"] = new
            {
                layout_root = "Grid",
                layout_description = "Header row (Auto), then UniformGrid or WrapPanel for metric cards, then a data area.",
                sample_components = new[]
                {
                    new { type = "TextBlock",    placement = "Row 0", notes = "Dashboard title" },
                    new { type = "WrapPanel",    placement = "Row 1", notes = "KPI card container" },
                    new { type = "DataGrid",     placement = "Row 2", notes = "Main data table" }
                }
            },
            ["toolbar-content"] = new
            {
                layout_root = "DockPanel",
                layout_description = "ToolBar docked to top, content fills remaining space.",
                sample_components = new[]
                {
                    new { type = "ToolBar",  placement = "DockPanel.Dock=Top", notes = "Action buttons" },
                    new { type = "Grid",     placement = "Fills remaining",    notes = "Main content" }
                }
            },
            ["header-content-footer"] = new
            {
                layout_root = "DockPanel",
                layout_description = "Header docked top, footer docked bottom, content fills middle.",
                sample_components = new[]
                {
                    new { type = "Border",       placement = "DockPanel.Dock=Top",    notes = "Page header" },
                    new { type = "Border",       placement = "DockPanel.Dock=Bottom", notes = "Footer with buttons" },
                    new { type = "ScrollViewer", placement = "Fills remaining",       notes = "Main content" }
                }
            },
            ["settings-page"] = new
            {
                layout_root = "Grid",
                layout_description = "Category list on left (200px), settings panel on right (*).",
                sample_components = new[]
                {
                    new { type = "ListBox",    placement = "Col 0", notes = "Settings categories" },
                    new { type = "StackPanel", placement = "Col 1", notes = "Settings for selected category" }
                }
            },
            ["card-list"] = new
            {
                layout_root = "StackPanel or ScrollViewer > StackPanel",
                layout_description = "Vertical stack of Border cards, each with title, subtitle, and action.",
                sample_components = new[]
                {
                    new { type = "Border",     placement = "Each card",       notes = "CornerRadius=8, Margin=4, Padding=12" },
                    new { type = "StackPanel", placement = "Inside Border",   notes = "Vertical content" },
                    new { type = "TextBlock",  placement = "Row 0 in card",   notes = "Card title, FontWeight=Bold" },
                    new { type = "TextBlock",  placement = "Row 1 in card",   notes = "Card subtitle, Opacity=0.7" }
                }
            },
            ["wizard"] = new
            {
                layout_root = "Grid",
                layout_description = "Step indicator row (Auto), content area (*), navigation buttons row (Auto).",
                sample_components = new[]
                {
                    new { type = "StackPanel", placement = "Row 0, Horizontal", notes = "Step indicators" },
                    new { type = "Border",     placement = "Row 1",             notes = "Current step content" },
                    new { type = "StackPanel", placement = "Row 2, Horizontal, Right-aligned", notes = "Back/Next buttons" }
                }
            }
        };

        var res = snippets.TryGetValue(screenType, out var s)
            ? JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = false })
            : $"Unknown screen type '{screenType}'. Available: {string.Join(", ", snippets.Keys)}";

        logs.Log("Planner:Tool", "tool", $"🔧 GetLayoutSnippet returning {(snippets.ContainsKey(screenType) ? $"layout snippet for screen type '{screenType}'" : $"error message for unknown screen type '{screenType}'")}");

        return res;
    }
}