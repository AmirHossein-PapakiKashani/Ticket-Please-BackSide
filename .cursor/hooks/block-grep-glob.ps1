$ErrorActionPreference = 'SilentlyContinue'
$allowed = '\.(razor|json|css|scss|md|mdc|ya?ml|toml|txt|config|props|targets|editorconfig|http|xml|csproj|sln|slnx|html|svg)$'
$inputText = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($inputText)) { Write-Output '{}'; exit 0 }
try { $payload = $inputText | ConvertFrom-Json } catch { Write-Output '{}'; exit 0 }
if ($payload.tool_name -in @('Grep','Glob') -and "$($payload.tool_input)" -notmatch $allowed) {
  @{ permission = 'deny'; user_message = 'Use codebase-memory MCP for source-code discovery.' } | ConvertTo-Json -Compress
  exit 0
}
Write-Output '{}'
