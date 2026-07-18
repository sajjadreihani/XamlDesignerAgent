using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;

namespace XamlRenderer;

public partial class App : Application
{
    private HttpListener? _listener;
    private CancellationTokenSource _cts = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        MainWindow = new MainWindow();

        Task.Run(() => StartHttpListenerAsync(_cts.Token));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cts.Cancel();
        _listener?.Stop();
        base.OnExit(e);
    }

    private async Task StartHttpListenerAsync(CancellationToken ct)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:5099/");
        _listener.Start();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch (HttpListenerException) { break; }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath;
        var method = context.Request.HttpMethod;

        if (method == "GET" && path == "/health")
        {
            await WriteJsonAsync(context, new { status = "ready" });
            return;
        }

        if (method != "POST")
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        string xaml;
        using (var reader = new StreamReader(context.Request.InputStream))
            xaml = await reader.ReadToEndAsync();

        object result = path switch
        {
            "/render" => await RenderXamlAsync(xaml),
            "/validate" => ValidateXaml(xaml),
            "/format" => FormatXaml(xaml),
            "/health" => new { status = "ready" },
            _ => null!
        };

        if (result is null)
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        await WriteJsonAsync(context, result);
    }

    private ValidationResult ValidateXaml(string xaml)
    {
        ValidationResult? result = null;

        Dispatcher.Invoke(() =>
        {
            try
            {
                XamlReader.Parse(xaml);
                result = new ValidationResult { Valid = true };
            }
            catch (XamlParseException ex)
            {
                result = new ValidationResult
                {
                    Valid = false,
                    Error = ex.Message,
                    Line = ex.LineNumber,
                    Position = ex.LinePosition
                };
            }
            catch (Exception ex)
            {
                result = new ValidationResult { Valid = false, Error = ex.Message };
            }
        });

        return result!;
    }

    private static FormatResult FormatXaml(string xaml)
    {
        try
        {
            var doc = XDocument.Parse(xaml);
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                OmitXmlDeclaration = true,
                NewLineOnAttributes = false
            };
            using var sw = new StringWriter();
            using var xw = XmlWriter.Create(sw, settings);
            doc.Save(xw);
            return new FormatResult { Success = true, FormattedXaml = sw.ToString() };
        }
        catch (Exception ex)
        {
            return new FormatResult { Success = false, Error = ex.Message };
        }
    }

    private Task<RenderResult> RenderXamlAsync(string xaml)
    {
        var tcs = new TaskCompletionSource<RenderResult>();

        // Must render on WPF UI thread
        Dispatcher.Invoke(() =>
        {
            try
            {
                var window = (Window)XamlReader.Parse(xaml);

                var content = (FrameworkElement)window.Content;

                content.Measure(new Size(900, 600));
                content.Arrange(new Rect(0, 0, 900, 600));
                content.UpdateLayout();

                //// Measure and arrange
                //var size = new Size(900, 600);
                //element.Measure(size);
                //element.Arrange(new Rect(size));
                //element.UpdateLayout();

                // Capture
                var bitmap = new RenderTargetBitmap(
                    900, 600, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using var ms = new MemoryStream();
                encoder.Save(ms);
                var base64 = Convert.ToBase64String(ms.ToArray());

                tcs.SetResult(new RenderResult { Success = true, ImageBase64 = base64 });
            }
            catch (Exception ex)
            {
                tcs.SetResult(new RenderResult { Success = false, Error = ex.Message });
            }
        });

        return tcs.Task;
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, object result)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 200;
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result));
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
}

public record RenderResult
{
    public bool Success { get; init; }
    public string? ImageBase64 { get; init; }
    public string? Error { get; init; }
}

public record ValidationResult
{
    public bool Valid { get; init; }
    public string? Error { get; init; }
    public int? Line { get; init; }
    public int? Position { get; init; }
}

public record FormatResult
{
    public bool Success { get; init; }
    public string? FormattedXaml { get; init; }
    public string? Error { get; init; }
}