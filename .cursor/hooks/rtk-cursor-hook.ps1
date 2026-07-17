$ErrorActionPreference = 'SilentlyContinue'
$inputText = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($inputText)) { Write-Output '{}'; exit 0 }
try {
    $output = $inputText | & rtk hook cursor 2>&1
    $text = ($output | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { Write-Output '{}' } else { Write-Output $text }
} catch { Write-Output '{}' }
