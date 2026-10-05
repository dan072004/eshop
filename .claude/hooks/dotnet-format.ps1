# PreToolUse hook: before Claude runs `git commit`, runs `dotnet format whitespace` on the staged C# files
# and re-stages them so the commit includes the formatting.
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
if ($payload.tool_input.command -notmatch '\bgit\s+commit\b') { exit 0 }

$root = $env:CLAUDE_PROJECT_DIR
if (-not $root) { $root = (Get-Location).Path }
Set-Location $root

$files = @(git diff --cached --name-only --diff-filter=ACMR -- '*.cs' | Where-Object { Test-Path -LiteralPath $_ })
if ($files.Count -eq 0) { exit 0 }

# --folder skips loading/restoring projects, so this stays fast; .editorconfig rules still apply.
dotnet format whitespace . --folder --include $files
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
git add -- $files
exit 0
