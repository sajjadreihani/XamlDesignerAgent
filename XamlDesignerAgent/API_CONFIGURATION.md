API_CONFIGURATION.md

This file documents how to configure API keys, model names, and the renderer URL for local development and deployment.

1) AI provider (OpenRouter)
- Obtain an API key from https://openrouter.ai and set it securely. Do NOT store real keys in appsettings.json in the repository.
- Locally you can set the key using `dotnet user-secrets` or environment variables.

Using environment variables (preferred for CI/CD):
- OpenRouterSettings__BaseUrl (default: https://openrouter.ai/api/v1)
- OpenRouterSettings__Key (your API key)

Example (PowerShell):
$env:OpenRouterSettings__BaseUrl = "https://openrouter.ai/api/v1"
$env:OpenRouterSettings__Key = "sk-..."

Or use dotnet user-secrets (for development inside the project directory):
> dotnet user-secrets set "OpenRouterSettings:Key" "sk-..."
> dotnet user-secrets set "OpenRouterSettings:BaseUrl" "https://openrouter.ai/api/v1"

2) Renderer service
- The local WPF renderer listens by default on http://localhost:5099. If you run the XamlRenderer separately or in Docker, update the URL.
- Config key: Renderer:Url
- Example: Renderer__Url=http://renderer-host:5099

3) Models and fallbacks
- The app uses free-tier OpenRouter models by default. If free models are unreliable, configure alternative models in appsettings.json or via environment variables.
- Config section: Models
  - Designer: model id for XAML generation (default: poolside/laguna-m.1:free)
  - Planner: model id for planning (default: openrouter/owl-alpha)
  - Reviewer: model id for verification (default: openrouter/owl-alpha)
  - Fallbacks: array of model ids used when the primary model fails

Examples:
- appsettings.json
{
  "Models": {
	"Designer": "poolside/laguna-m.1:free",
	"Planner": "openrouter/owl-alpha",
	"Reviewer": "openrouter/owl-alpha",
	"Fallbacks": [ "openai/gpt-oss-20b:free", "openai/gpt-oss-120b:free" ]
  }
}

4) Safety notes
- Never commit real API keys to source control. If you accidentally commit a key, rotate it and remove it from history.
- Use .gitignore to exclude local secrets files (.env, appsettings.*.json, secrets.json).

5) Troubleshooting
- If a model times out or returns malformed JSON, the app will attempt configured fallback models.
- If the renderer is offline, validation and rendering features will be disabled and a friendly message will be shown in the UI.

