# UI smoke test for CI (Windows PowerShell 5.1): launches the app, fills fields through UI Automation,
# saves window screenshots and the list of keyboard-focusable elements (names, help texts).
# This is not a screen reader test: it only proves the window starts and renders.
# ASCII only: Windows PowerShell reads BOM-less scripts in the ANSI code page.
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$OutDir
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
"@

$A = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Save-Shot([string]$name) {
    $r = New-Object Win32+RECT
    [Win32]::GetWindowRect($script:hwnd, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    $bmp.Save((Join-Path $OutDir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

function Find-Id([string]$id) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, $id)
    $e = $script:root.FindFirst($Scope::Descendants, $cond)
    if (-not $e) { throw "Element not found: $id" }
    return $e
}

function Set-Text([string]$id, [string]$value) {
    (Find-Id $id).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($value)
}

function Set-Finger([int]$n, [string]$fret, [string]$str) {
    Set-Text "Finger${n}Fret" $fret
    Set-Text "Finger${n}String" $str
}

function Select-ComboIndex([string]$id, [int]$index) {
    $combo = Find-Id $id
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Start-Sleep -Milliseconds 300
    $cond = New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    $items = $combo.FindAll($Scope::Descendants, $cond)
    $items[$index].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
}

function Select-Radio([int]$index) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::RadioButton)
    $radios = $script:root.FindAll($Scope::Descendants, $cond)
    $radios[$index].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
}

$proc = Start-Process -FilePath $Exe -PassThru
$deadline = (Get-Date).AddSeconds(90)
do {
    Start-Sleep -Milliseconds 500
    $proc.Refresh()
} while ($proc.MainWindowHandle -eq [IntPtr]::Zero -and -not $proc.HasExited -and (Get-Date) -lt $deadline)

if ($proc.HasExited) { throw "App exited during startup, code $($proc.ExitCode)" }
if ($proc.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Main window did not appear in 90 s' }

$script:hwnd = $proc.MainWindowHandle
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$width = [Math]::Min(1360, $screen.Width)
$height = [Math]::Min(900, $screen.Height)
[Win32]::MoveWindow($script:hwnd, $screen.Left, $screen.Top, $width, $height, $true) | Out-Null
[Win32]::SetForegroundWindow($script:hwnd) | Out-Null
Start-Sleep -Seconds 2

$title = $proc.MainWindowTitle
Write-Host "Window title: $title; screen $($screen.Width)x$($screen.Height)"
$script:root = $A::FromHandle($script:hwnd)

try {
    # The unhandled-exception handler shows a message box titled "MajorMode: ..." instead of the main window.
    if (-not $title.StartsWith('MajorMode ')) { throw "Unexpected window: $title" }
    Find-Id 'ChordNameBox' | Out-Null
    Save-Shot '01-start'

    Set-Text 'ChordNameBox' 'C'
    Set-Finger 1 '1' '2'
    Set-Finger 2 '2' '4'
    Set-Finger 3 '3' '5'
    Set-Text 'MutedBox' '6'
    Start-Sleep -Seconds 2
    Save-Shot '02-chord-c'

    (Find-Id 'SaveButton').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 3
    Save-Shot '02b-saved'

    Set-Finger 4 '0' '3'
    Start-Sleep -Seconds 3
    Save-Shot '03-error'

    Set-Finger 4 '0' '0'
    Set-Text 'ChordNameBox' 'F'
    Set-Finger 1 '1' 'B'
    Set-Finger 2 '2' '3'
    Set-Finger 3 '3' '5'
    Set-Finger 4 '3' '4'
    Set-Text 'MutedBox' ''
    Select-ComboIndex 'StyleBox' 2
    Select-Radio 1
    Start-Sleep -Seconds 2
    Save-Shot '04-f-wood-horizontal'

    Select-ComboIndex 'StyleBox' 1
    Select-ComboIndex 'CanvasBox' 1
    Select-Radio 0
    Start-Sleep -Seconds 2
    Save-Shot '05-modern-portrait'

    (Find-Id 'CheckUpdatesButton').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 6
    Save-Shot '06-update-check'

    $all = $script:root.FindAll($Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $lines = foreach ($e in $all) {
        $c = $e.Current
        if ($c.IsKeyboardFocusable) {
            "{0}`t{1}`t{2}`t{3}" -f $c.ControlType.ProgrammaticName, $c.AutomationId, $c.Name, $c.HelpText
        }
    }
    $lines | Out-File -FilePath (Join-Path $OutDir 'focusable-elements.txt') -Encoding UTF8
}
catch {
    Save-Shot 'failure'
    throw
}
finally {
    if (-not $proc.HasExited) { $proc.Kill() }
}
