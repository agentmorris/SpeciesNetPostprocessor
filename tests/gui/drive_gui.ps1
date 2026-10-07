# Drive the SpeciesNet Postprocessor GUI: set the input/output files, click Process, wait for
# the completion message box, and capture screenshots along the way.  Useful as a smoke test of
# a published .exe.
#
# The program's saved settings (%APPDATA%\SpeciesNetPostprocessor\settings.json) are backed up
# before the test and restored afterwards.
#
# Buttons are clicked by posting BM_CLICK messages (UI Automation's Invoke blocks on buttons that
# open modal dialogs), and windows are found with EnumWindows.
#
# Usage:
#
#   drive_gui.ps1 -Exe <exe> -InputFile <json> -ScreenshotFolder <dir> [-OutputFile <json>]
#                 [-Prefix <name>] [-Advanced] [-UncheckSequence] [-CancelAfterSeconds <n>]
#                 [-TimeoutSeconds <n>]
#
#   -Advanced: open the advanced options dialog (and capture it) before processing
#   -CancelAfterSeconds: click Cancel this many seconds after starting processing

param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$InputFile,
    [Parameter(Mandatory = $true)][string]$ScreenshotFolder,
    [string]$OutputFile = "",
    [string]$Prefix = "run",
    [switch]$Advanced,
    [switch]$UncheckSequence,
    [double]$CancelAfterSeconds = -1,
    [int]$TimeoutSeconds = 600
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Win32
{
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern IntPtr GetDlgItem(IntPtr hwnd, int id);
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    public static string GetText(IntPtr hwnd)
    {
        var sb = new StringBuilder(4096);
        GetWindowText(hwnd, sb, 4096);
        return sb.ToString();
    }

    // Finds a visible window belonging to [processId] whose title starts with [titlePrefix] (or
    // equals it, if [exact] is true), optionally restricted to (or excluding) a window class.
    public static IntPtr FindWindow(uint processId, string titlePrefix, bool exact, string className, bool excludeClass)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, l) =>
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid != processId || !IsWindowVisible(hwnd)) return true;
            string title = GetText(hwnd);
            var cls = new StringBuilder(256);
            GetClassName(hwnd, cls, 256);
            bool titleMatches = exact ? title == titlePrefix : title.StartsWith(titlePrefix);
            bool classMatches = string.IsNullOrEmpty(className) || ((cls.ToString() == className) != excludeClass);
            if (titleMatches && classMatches)
            {
                found = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@

$screenshot = Join-Path $PSScriptRoot "screenshot_window.ps1"
$A = [System.Windows.Automation.AutomationElement]
$TreeScope = [System.Windows.Automation.TreeScope]
$BM_CLICK = 0x00F5
$DialogClass = "#32770"
$AppTitle = "SpeciesNet Postprocessor"

function Find-ById($root, $id) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, $id)
    return $root.FindFirst($TreeScope::Descendants, $cond)
}

function Wait-Window($processId, $title, $exact, $className, $excludeClass, $timeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $hwnd = [Win32]::FindWindow([uint32]$processId, $title, $exact, $className, $excludeClass)
        if ($hwnd -ne [IntPtr]::Zero) { return $hwnd }
        Start-Sleep -Milliseconds 250
    }
    return [IntPtr]::Zero
}

# Message boxes have the title "SpeciesNet Postprocessor"; the main window's title also includes
# the version number
function Wait-MessageBox($processId, $timeoutSeconds) {
    return Wait-Window $processId $AppTitle $true $DialogClass $false $timeoutSeconds
}

function Save-Screenshot($hwnd, $name) {
    & $screenshot -Hwnd ([long]$hwnd) -OutFile (Join-Path $ScreenshotFolder "$Prefix-$name.png")
}

function Set-Text($root, $id, $text) {
    (Find-ById $root $id).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text)
}

function Click($root, $id) {
    $el = Find-ById $root $id
    [void][Win32]::PostMessage([IntPtr]$el.Current.NativeWindowHandle, $BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero)
}

function Get-MessageBoxText($hwnd) {
    # The text of a standard message box is the static control with ID 0xFFFF
    return [Win32]::GetText([Win32]::GetDlgItem($hwnd, 0xFFFF))
}

function Click-MessageBoxButton($hwnd, $name) {
    # In a message box with only an OK button, that button has the ID of a Cancel button
    $ids = @{ "OK" = @(1, 2); "Cancel" = @(2); "Yes" = @(6); "No" = @(7) }
    foreach ($id in $ids[$name]) {
        $button = [Win32]::GetDlgItem($hwnd, $id)
        if ($button -ne [IntPtr]::Zero) {
            [void][Win32]::PostMessage($button, $BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero)
            return
        }
    }
    Write-Error "Could not find the $name button"
}

New-Item -ItemType Directory -Force $ScreenshotFolder | Out-Null

# Back up the program's saved settings
$settingsFile = Join-Path $env:APPDATA "SpeciesNetPostprocessor\settings.json"
$settingsBackup = $null
if (Test-Path $settingsFile) {
    $settingsBackup = [System.IO.File]::ReadAllBytes($settingsFile)
}

$p = Start-Process -FilePath $Exe -PassThru

try {
    [void]$p.WaitForInputIdle(15000)
    $mainHwnd = Wait-Window $p.Id "$AppTitle " $false $DialogClass $true 15
    if ($mainHwnd -eq [IntPtr]::Zero) { throw "Could not find the main window" }
    $main = $A::FromHandle($mainHwnd)

    if ($Advanced) {
        Click $main "buttonAdvanced"
        $advancedHwnd = Wait-Window $p.Id "Advanced options" $true $null $false 10
        Start-Sleep -Milliseconds 500
        Save-Screenshot $advancedHwnd "advanced"
        Click $A::FromHandle($advancedHwnd) "buttonCancel"
        Start-Sleep -Milliseconds 500
    }

    Set-Text $main "textInputFile" $InputFile
    Set-Text $main "textOutputFile" $OutputFile
    if ($UncheckSequence) {
        $toggle = (Find-ById $main "checkSequenceSmoothing").GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        if ($toggle.Current.ToggleState -eq "On") { $toggle.Toggle() }
    }
    Click $main "buttonProcess"

    # The first message box might be the "output file exists" prompt
    $boxHwnd = Wait-MessageBox $p.Id 3
    if ($boxHwnd -ne [IntPtr]::Zero) {
        $text = Get-MessageBoxText $boxHwnd
        if ($text -match "already exists") {
            Write-Output "Overwrite prompt: $text"
            Save-Screenshot $boxHwnd "overwrite"
            Click-MessageBoxButton $boxHwnd "Yes"
            Start-Sleep -Milliseconds 500
            $boxHwnd = [IntPtr]::Zero
        }
    }

    if ($CancelAfterSeconds -ge 0) {
        Start-Sleep -Milliseconds ([int]($CancelAfterSeconds * 1000))
        Save-Screenshot $mainHwnd "running"
        Click $main "buttonCancel"
        Start-Sleep -Seconds 3
        Save-Screenshot $mainHwnd "cancelled"
    }
    else {
        if ($boxHwnd -eq [IntPtr]::Zero) {
            Start-Sleep -Milliseconds 1500
            Save-Screenshot $mainHwnd "running"
            $boxHwnd = Wait-MessageBox $p.Id $TimeoutSeconds
        }
        if ($boxHwnd -eq [IntPtr]::Zero) { throw "Timed out waiting for processing to finish" }
        Start-Sleep -Milliseconds 500
        Write-Output "Message box: $(Get-MessageBoxText $boxHwnd)"
        Save-Screenshot $boxHwnd "messagebox"
        Click-MessageBoxButton $boxHwnd "OK"
        Start-Sleep -Milliseconds 500
        Save-Screenshot $mainHwnd "finished"
    }

    $log = (Find-ById $main "textLog").GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    Write-Output "---- log pane ----"
    Write-Output $log

    [void]$p.CloseMainWindow()
    $exitedCleanly = $p.WaitForExit(10000)
    Write-Output "Exited cleanly: $exitedCleanly"
}
finally {
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }

    # Restore the program's saved settings
    if ($settingsBackup -ne $null) {
        [System.IO.File]::WriteAllBytes($settingsFile, $settingsBackup)
    }
    elseif (Test-Path $settingsFile) {
        Remove-Item $settingsFile
    }
}
