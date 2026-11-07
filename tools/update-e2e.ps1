# End-to-end update test for CI (Windows PowerShell 5.1), runs after the new release is published:
# downloads the previous release, starts it, clicks "check updates", confirms the dialog and verifies
# that the exe was replaced by the new version and restarted.
# ASCII only: Windows PowerShell reads BOM-less scripts in the ANSI code page.
param(
    [Parameter(Mandatory = $true)][string]$ExpectedVersion,
    [Parameter(Mandatory = $true)][string]$OutDir
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32E2E {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
}
"@

$A = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Save-Shot([IntPtr]$hwnd, [string]$name) {
    $r = New-Object Win32E2E+RECT
    [Win32E2E]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    if ($w -le 0 -or $h -le 0) { return }
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    $bmp.Save((Join-Path $OutDir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

function Wait-MainWindow([System.Diagnostics.Process]$p, [int]$seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        Start-Sleep -Milliseconds 500
        $p.Refresh()
    } while ($p.MainWindowHandle -eq [IntPtr]::Zero -and -not $p.HasExited -and (Get-Date) -lt $deadline)
    if ($p.HasExited -or $p.MainWindowHandle -eq [IntPtr]::Zero) { throw "Window of process $($p.Id) did not appear" }
    Start-Sleep -Seconds 2
}

$tags = gh release list --limit 20 --json tagName,createdAt --jq 'sort_by(.createdAt) | reverse | .[].tagName'
$tags = @($tags)
if ($tags.Count -lt 2) {
    Write-Host 'Less than two releases: nothing to update from yet, skipping.'
    exit 0
}

$previous = $tags[1]

# The public API caches releases/latest for about a minute; wait until it reports the new release.
$repo = $env:GITHUB_REPOSITORY
$deadline = (Get-Date).AddMinutes(5)
do {
    try {
        $latest = (Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -UseBasicParsing `
            -Headers @{ 'User-Agent' = 'MajorMode-e2e'; 'Accept' = 'application/vnd.github+json' }).tag_name
    } catch { $latest = $null }
    Write-Host "Public API latest: $latest"
    if ($latest -eq "v$ExpectedVersion") { break }
    Start-Sleep -Seconds 15
} while ((Get-Date) -lt $deadline)
if ($latest -ne "v$ExpectedVersion") { throw "Public API still reports $latest" }
$dir = Join-Path (Get-Location) 'e2e'
Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $dir | Out-Null
gh release download $previous --pattern 'MajorMode.exe' --dir $dir
$exe = Join-Path $dir 'MajorMode.exe'
$before = (Get-Item $exe).VersionInfo.FileVersion
Write-Host "Previous release $previous, file version $before; expecting $ExpectedVersion"

$old = Start-Process -FilePath $exe -PassThru
$newProc = $null
try {
    Wait-MainWindow $old 90
    $root = $A::FromHandle($old.MainWindowHandle)
    $cond = New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, 'CheckUpdatesButton')
    $button = $root.FindFirst($Scope::Descendants, $cond)
    if (-not $button) { throw 'CheckUpdatesButton not found (previous release may predate the updater)' }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()

    # Confirmation message box (#32770); "Yes" has AutomationId 6 (IDYES).
    $yes = $null
    $deadline = (Get-Date).AddSeconds(60)
    $pidCond = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $old.Id)
    $yesCond = New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, '6')
    while (-not $yes -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        foreach ($w in $A::RootElement.FindAll($Scope::Children, $pidCond)) {
            $yes = $w.FindFirst($Scope::Descendants, $yesCond)
            if ($yes) {
                Save-Shot ([IntPtr]$w.Current.NativeWindowHandle) 'e2e-01-confirm'
                break
            }
        }
    }
    if (-not $yes) {
        Save-Shot $old.MainWindowHandle 'e2e-failure'
        throw 'Update confirmation dialog did not appear'
    }
    $yes.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()

    if (-not $old.WaitForExit(300000)) {
        Save-Shot $old.MainWindowHandle 'e2e-failure'
        throw 'Old version did not exit after update'
    }

    $after = (Get-Item $exe).VersionInfo.FileVersion
    Write-Host "File version after update: $after"
    if ($after -ne "$ExpectedVersion.0") { throw "Exe not replaced: $after" }

    $deadline = (Get-Date).AddSeconds(60)
    while (-not $newProc -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $newProc = Get-Process -Name 'MajorMode' -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -eq $exe } | Select-Object -First 1
    }
    if (-not $newProc) { throw 'New version was not restarted' }
    Wait-MainWindow $newProc 90
    Start-Sleep -Seconds 3
    Save-Shot $newProc.MainWindowHandle 'e2e-02-updated'

    Start-Sleep -Seconds 3
    if (Test-Path "$exe.old") { throw 'Old exe was not cleaned up' }
    Write-Host "Update OK: $before -> $after"
}
finally {
    if (-not $old.HasExited) { $old.Kill() }
    if ($newProc -and -not $newProc.HasExited) { $newProc.Kill() }
}
