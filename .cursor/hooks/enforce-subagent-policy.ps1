$inputText = [Console]::In.ReadToEnd()
@{ permission = 'allow'; agent_message = 'Use codebase-memory MCP for C# discovery. Prefer rtk for shell commands. Do not use grep/glob for C# source.' } | ConvertTo-Json -Compress
