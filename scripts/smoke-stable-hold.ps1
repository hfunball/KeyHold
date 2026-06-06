param(
    [string]$ExecutablePath = (Join-Path $PSScriptRoot '..\src\KeyHold\bin\Debug\net10.0-windows\KeyHold.exe')
)

$ErrorActionPreference = 'Stop'

function Wait-For {
    param(
        [scriptblock]$Probe,
        [string]$Description,
        [int]$TimeoutSeconds = 10
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $result = & $Probe
        if ($null -ne $result -and $false -ne $result) {
            return $result
        }

        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for $Description."
}

if (!(Test-Path -LiteralPath $ExecutablePath)) {
    throw "KeyHold executable was not found at $ExecutablePath. Run the build first."
}

$existing = Get-Process -Name KeyHold -ErrorAction SilentlyContinue
if ($existing) {
    throw 'Close any running KeyHold process before running this smoke test.'
}

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class KeyHoldStableSmokeInput
{
    private const uint KeyEventFKeyUp = 0x0002;

    public static void KeyDown(byte virtualKey)
    {
        keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
    }

    public static void KeyUp(byte virtualKey)
    {
        keybd_event(virtualKey, 0, KeyEventFKeyUp, UIntPtr.Zero);
    }

    public static bool IsDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
'@

$settingsPath = Join-Path $env:LOCALAPPDATA 'KeyHold\settings.json'
$settingsFolder = Split-Path -Parent $settingsPath
$hadSettings = Test-Path -LiteralPath $settingsPath
$originalSettings = if ($hadSettings) { Get-Content -LiteralPath $settingsPath -Raw } else { $null }
$previousSmokeFlag = [Environment]::GetEnvironmentVariable('KEYHOLD_ACCEPT_EXTERNAL_INJECTED_INPUT_FOR_SMOKE', 'Process')
$keyHoldProcess = $null

$a = [byte]0x41
$s = [byte]0x53
$w = [byte]0x57
$homeKey = [byte]0x24

try {
    New-Item -ItemType Directory -Path $settingsFolder -Force | Out-Null
    $testSettings = @{
        ToggleBinding = @{ Device = 0; Code = 36; DisplayName = 'Home' }
        Theme = 0
        LaunchToTray = $false
        ShowNotifications = $false
        HasSeenFirstRun = $true
    } | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($settingsPath, $testSettings)

    [Environment]::SetEnvironmentVariable('KEYHOLD_ACCEPT_EXTERNAL_INJECTED_INPUT_FOR_SMOKE', '1', 'Process')
    $keyHoldStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $keyHoldStartInfo.FileName = $ExecutablePath
    $keyHoldStartInfo.UseShellExecute = $false
    $keyHoldStartInfo.EnvironmentVariables['KEYHOLD_ACCEPT_EXTERNAL_INJECTED_INPUT_FOR_SMOKE'] = '1'
    $keyHoldProcess = [System.Diagnostics.Process]::Start($keyHoldStartInfo)
    Wait-For { Get-Process -Id $keyHoldProcess.Id -ErrorAction SilentlyContinue } 'KeyHold process' | Out-Null
    Start-Sleep -Seconds 1

    [KeyHoldStableSmokeInput]::KeyDown($a)
    [KeyHoldStableSmokeInput]::KeyDown($s)
    [KeyHoldStableSmokeInput]::KeyDown($w)
    Start-Sleep -Milliseconds 150
    [KeyHoldStableSmokeInput]::KeyDown($homeKey)
    Start-Sleep -Milliseconds 40
    [KeyHoldStableSmokeInput]::KeyUp($homeKey)
    Start-Sleep -Milliseconds 100
    [KeyHoldStableSmokeInput]::KeyUp($a)
    [KeyHoldStableSmokeInput]::KeyUp($s)
    [KeyHoldStableSmokeInput]::KeyUp($w)
    Start-Sleep -Milliseconds 150

    $samples = 0
    $allDownSamples = 0
    for ($i = 0; $i -lt 25; $i++) {
        $allDown = [KeyHoldStableSmokeInput]::IsDown($a) -and [KeyHoldStableSmokeInput]::IsDown($s) -and [KeyHoldStableSmokeInput]::IsDown($w)
        if ($allDown) {
            $allDownSamples++
        }

        $samples++
        Start-Sleep -Milliseconds 20
    }

    if ($allDownSamples -lt 22) {
        throw "Stable hold smoke failed. Expected A/S/W to stay down after physical release; saw all three down in $allDownSamples of $samples samples."
    }

    [KeyHoldStableSmokeInput]::KeyDown($homeKey)
    Start-Sleep -Milliseconds 40
    [KeyHoldStableSmokeInput]::KeyUp($homeKey)
    Start-Sleep -Milliseconds 200

    $anyStillDown = [KeyHoldStableSmokeInput]::IsDown($a) -or [KeyHoldStableSmokeInput]::IsDown($s) -or [KeyHoldStableSmokeInput]::IsDown($w)
    if ($anyStillDown) {
        throw 'Stable hold smoke failed. At least one held key was still down after Home stop.'
    }

    [KeyHoldStableSmokeInput]::KeyDown($a)
    [KeyHoldStableSmokeInput]::KeyDown($s)
    [KeyHoldStableSmokeInput]::KeyDown($w)
    Start-Sleep -Milliseconds 150
    [KeyHoldStableSmokeInput]::KeyDown($homeKey)
    Start-Sleep -Milliseconds 40
    [KeyHoldStableSmokeInput]::KeyUp($homeKey)
    Start-Sleep -Milliseconds 100
    [KeyHoldStableSmokeInput]::KeyUp($a)
    [KeyHoldStableSmokeInput]::KeyUp($s)
    [KeyHoldStableSmokeInput]::KeyUp($w)
    Start-Sleep -Milliseconds 150

    $handoffSamples = 0
    $handoffDownSamples = 0
    for ($i = 0; $i -lt 25; $i++) {
        $allDown = [KeyHoldStableSmokeInput]::IsDown($a) -and [KeyHoldStableSmokeInput]::IsDown($s) -and [KeyHoldStableSmokeInput]::IsDown($w)
        if ($allDown) {
            $handoffDownSamples++
        }

        $handoffSamples++
        Start-Sleep -Milliseconds 20
    }

    if ($handoffDownSamples -lt 22) {
        throw "Stable hold smoke failed. Expected A/S/W to stay down before physical handoff; saw all three down in $handoffDownSamples of $handoffSamples samples."
    }

    [KeyHoldStableSmokeInput]::KeyDown($w)
    Start-Sleep -Milliseconds 40
    [KeyHoldStableSmokeInput]::KeyUp($w)
    Start-Sleep -Milliseconds 200

    $anyStillDown = [KeyHoldStableSmokeInput]::IsDown($a) -or [KeyHoldStableSmokeInput]::IsDown($s) -or [KeyHoldStableSmokeInput]::IsDown($w)
    if ($anyStillDown) {
        throw 'Stable hold smoke failed. At least one held key was still down after physical handoff.'
    }

    'KeyHold stable-hold smoke passed: Home toggle held A/S/W after physical release, Home stopped them, and physical W handoff released them.'
}
finally {
    [Environment]::SetEnvironmentVariable('KEYHOLD_ACCEPT_EXTERNAL_INJECTED_INPUT_FOR_SMOKE', $previousSmokeFlag, 'Process')

    foreach ($key in @($a, $s, $w, $homeKey)) {
        [KeyHoldStableSmokeInput]::KeyUp($key)
    }

    if ($keyHoldProcess -and -not $keyHoldProcess.HasExited) {
        Stop-Process -Id $keyHoldProcess.Id -Force
    }

    if ($hadSettings) {
        [System.IO.File]::WriteAllText($settingsPath, $originalSettings)
    }
    else {
        Remove-Item -LiteralPath $settingsPath -Force -ErrorAction SilentlyContinue
    }
}
