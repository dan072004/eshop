# Verification gate for the `verify` skill.
#   PreToolUse hook: before Claude runs `git commit`, denies it if there are uncommitted code changes
#                    that differ from what the verify skill last stamped as passing.
#   -Stamp:          run by the verify skill after a passing report; records the current changes as verified.
# The fingerprint is each changed code file's path plus a hash of its working-tree content, so staging
# (`git add`) doesn't invalidate it but any further edit does.
param([switch]$Stamp)

$root = $env:CLAUDE_PROJECT_DIR
if (-not $root) { $root = (git rev-parse --show-toplevel 2>$null) }
if (-not $root) { $root = (Get-Location).Path }
Set-Location $root

$stateDir = Join-Path $root 'artifacts/.verify'
$stampFile = Join-Path $stateDir 'stamp'
$codePaths = @('src', 'tests', 'e2e', '*.props', 'global.json', 'eShop.Web.slnf')

function Get-CodeFingerprint {
    $changed = @(git diff HEAD --name-only -- $codePaths 2>$null) +
        @(git ls-files --others --exclude-standard -- $codePaths 2>$null) | Sort-Object -Unique
    if ($changed.Count -eq 0) { return 'clean' }

    $entries = $changed | ForEach-Object {
        if (Test-Path -LiteralPath $_) { "$_ $(git hash-object -- $_)" } else { "$_ deleted" }
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes($entries -join "`n")
    $sha = [Security.Cryptography.SHA256]::Create()
    return [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '')
}

if ($Stamp) {
    New-Item -ItemType Directory -Force $stateDir | Out-Null
    $fingerprint = Get-CodeFingerprint
    Set-Content -LiteralPath $stampFile -Value $fingerprint -Encoding ascii
    Write-Output "Verification stamp recorded ($fingerprint)."
    exit 0
}

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
if ($payload.tool_input.command -notmatch '\bgit\s+commit\b') { exit 0 }

$fingerprint = Get-CodeFingerprint
if ($fingerprint -eq 'clean') { exit 0 }

$stamped = if (Test-Path -LiteralPath $stampFile) { (Get-Content -LiteralPath $stampFile -Raw).Trim() } else { '' }
if ($fingerprint -eq $stamped) { exit 0 }

@{
    hookSpecificOutput = @{
        hookEventName            = 'PreToolUse'
        permissionDecision       = 'deny'
        permissionDecisionReason = 'Uncommitted code changes have not passed verification. Run the verify skill (.claude/skills/verify) before committing. If verification cannot pass, tell the user what failed instead of committing.'
    }
} | ConvertTo-Json -Compress -Depth 3
exit 0
