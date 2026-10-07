# Capture a window to a .png using PrintWindow (which works even if the window is covered by
# other windows).  Specify either a window handle, or a process ID and (part of) a window title.
#
# Usage: screenshot_window.ps1 -OutFile <png> (-Hwnd <handle> | -ProcessId <id> [-TitleContains <text>])

param(
    [Parameter(Mandatory = $true)][string]$OutFile,
    [long]$Hwnd = 0,
    [int]$ProcessId = 0,
    [string]$TitleContains = ""
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class WindowCapture
{
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int max);
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public static List<IntPtr> FindWindows(uint processId, string titleContains)
    {
        var result = new List<IntPtr>();
        EnumWindows((hwnd, l) =>
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == processId && IsWindowVisible(hwnd))
            {
                var sb = new StringBuilder(512);
                GetWindowText(hwnd, sb, 512);
                if (sb.ToString().Contains(titleContains)) result.Add(hwnd);
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
"@

if ($Hwnd -ne 0) {
    $handle = [IntPtr]$Hwnd
}
else {
    $windows = [WindowCapture]::FindWindows([uint32]$ProcessId, $TitleContains)
    if ($windows.Count -eq 0) { Write-Error "No matching window found"; exit 1 }
    $handle = $windows[0]
}

$rect = New-Object WindowCapture+RECT
[void][WindowCapture]::GetWindowRect($handle, [ref]$rect)
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
# PW_RENDERFULLCONTENT
[void][WindowCapture]::PrintWindow($handle, $hdc, 2)
$graphics.ReleaseHdc($hdc)
$graphics.Dispose()
$bitmap.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
Write-Output "Saved ${width}x${height} capture to $OutFile"
