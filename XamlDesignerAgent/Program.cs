using XamlDesignerAgent.AI;
using XamlDesignerAgent.Components;
using XamlDesignerAgent.Renderer.Interface;
using XamlDesignerAgent.Renderer.Services;
using Microsoft.Extensions.Options;
using XamlDesignerAgent.AI.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Bind Models options
builder.Services.Configure<ModelsOptions>(builder.Configuration.GetSection("Models"));

// Configure Xaml renderer HTTP client using configured Renderer:Url
var rendererUrl = builder.Configuration.GetValue<string>("Renderer:Url") ?? "http://localhost:5099";
builder.Services.AddHttpClient<XamlRenderService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.BaseAddress = new Uri(rendererUrl);
});
builder.Services.AddScoped<IXamlRenderer, XamlRenderService>();
builder.Services.AddAIService();

// Program.cs


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
