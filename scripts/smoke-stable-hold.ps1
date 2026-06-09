param(
    [string]$ExecutablePath = (Join-Path $PSScriptRoot '..\src\RunHold\bin\Debug\net10.0-windows\RunHold.exe')
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
    throw "RunHold executable was not found at $ExecutablePath. Run the build first."
}

$existing = Get-Process -Name RunHold -ErrorAction SilentlyContinue
if ($existing) {
    throw 'Close any running RunHold process before running this smoke test.'
}

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class RunHoldStableSmokeInput
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

$settingsPath = Join-Path $env:LOCALAPPDATA 'RunHold\settings.json'
$settingsFolder = Split-Path -Parent $settingsPath
$hadSettings = Test-Path -LiteralPath $settingsPath
$originalSettings = if ($hadSettings) { Get-Content -LiteralPath $settingsPath -Raw } else { $null }
$previousSmokeFlag = [Environment]::GetEnvironmentVariable('RUNHOLD_ACCEPT_EXTERNAL_INJECTED_INPUT_FOR_SMOKE', 'Process')
$runHoldProcess = $null

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

    [Environment]::SetEnvironmentVariable('RUNHOLD_ACCEPT_EXTERNAL_INJECTED_INPUT_FOR_SMOKE', '1', 'Process')
    $runHoldStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $runHoldStartInfo.FileName = $ExecutablePath
    $runHoldStartInfo.UseShellExecute = $false
    $runHoldStartInfo.EnvironmentVariables['RUNHOLD_ACCEPT_EXTERNAL_INJECTED_INPUT_FOR_SMOKE'] = '1'
    $runHoldProcess = [System.Diagnostics.Process]::Start($runHoldStartInfo)
    Wait-For { Get-Process -Id $runHoldProcess.Id -ErrorAction SilentlyContinue } 'RunHold process' | Out-Null
    Start-Sleep -Seconds 1

    [RunHoldStableSmokeInput]::KeyDown($a)
    [RunHoldStableSmokeInput]::KeyDown($s)
    [RunHoldStableSmokeInput]::KeyDown($w)
    Start-Sleep -Milliseconds 150
    [RunHoldStableSmokeInput]::KeyDown($homeKey)
    Start-Sleep -Milliseconds 40
    [RunHoldStableSmokeInput]::KeyUp($homeKey)
    Start-Sleep -Milliseconds 100
    [RunHoldStableSmokeInput]::KeyUp($a)
    [RunHoldStableSmokeInput]::KeyUp($s)
    [RunHoldStableSmokeInput]::KeyUp($w)
    Start-Sleep -Milliseconds 150

    $samples = 0
    $allDownSamples = 0
    for ($i = 0; $i -lt 25; $i++) {
        $allDown = [RunHoldStableSmokeInput]::IsDown($a) -and [RunHoldStableSmokeInput]::IsDown($s) -and [RunHoldStableSmokeInput]::IsDown($w)
        if ($allDown) {
            $allDownSamples++
        }

        $samples++
        Start-Sleep -Milliseconds 20
    }

    if ($allDownSamples -lt 22) {
        throw "Stable hold smoke failed. Expected A/S/W to stay down after physical release; saw all three down in $allDownSamples of $samples samples."
    }

    [RunHoldStableSmokeInput]::KeyDown($homeKey)
    Start-Sleep -Milliseconds 40
    [RunHoldStableSmokeInput]::KeyUp($homeKey)
    Start-Sleep -Milliseconds 200

    $anyStillDown = [RunHoldStableSmokeInput]::IsDown($a) -or [RunHoldStableSmokeInput]::IsDown($s) -or [RunHoldStableSmokeInput]::IsDown($w)
    if ($anyStillDown) {
        throw 'Stable hold smoke failed. At least one held key was still down after Home stop.'
    }

    [RunHoldStableSmokeInput]::KeyDown($a)
    [RunHoldStableSmokeInput]::KeyDown($s)
    [RunHoldStableSmokeInput]::KeyDown($w)
    Start-Sleep -Milliseconds 150
    [RunHoldStableSmokeInput]::KeyDown($homeKey)
    Start-Sleep -Milliseconds 40
    [RunHoldStableSmokeInput]::KeyUp($homeKey)
    Start-Sleep -Milliseconds 100
    [RunHoldStableSmokeInput]::KeyUp($a)
    [RunHoldStableSmokeInput]::KeyUp($s)
    [RunHoldStableSmokeInput]::KeyUp($w)
    Start-Sleep -Milliseconds 150

    $handoffSamples = 0
    $handoffDownSamples = 0
    for ($i = 0; $i -lt 25; $i++) {
        $allDown = [RunHoldStableSmokeInput]::IsDown($a) -and [RunHoldStableSmokeInput]::IsDown($s) -and [RunHoldStableSmokeInput]::IsDown($w)
        if ($allDown) {
            $handoffDownSamples++
        }

        $handoffSamples++
        Start-Sleep -Milliseconds 20
    }

    if ($handoffDownSamples -lt 22) {
        throw "Stable hold smoke failed. Expected A/S/W to stay down before physical handoff; saw all three down in $handoffDownSamples of $handoffSamples samples."
    }

    [RunHoldStableSmokeInput]::KeyDown($w)
    Start-Sleep -Milliseconds 40
    [RunHoldStableSmokeInput]::KeyUp($w)
    Start-Sleep -Milliseconds 200

    $anyStillDown = [RunHoldStableSmokeInput]::IsDown($a) -or [RunHoldStableSmokeInput]::IsDown($s) -or [RunHoldStableSmokeInput]::IsDown($w)
    if ($anyStillDown) {
        throw 'Stable hold smoke failed. At least one held key was still down after physical handoff.'
    }

    'RunHold stable-hold smoke passed: Home toggle held A/S/W after physical release, Home stopped them, and physical W handoff released them.'
}
finally {
    [Environment]::SetEnvironmentVariable('RUNHOLD_ACCEPT_EXTERNAL_INJECTED_INPUT_FOR_SMOKE', $previousSmokeFlag, 'Process')

    foreach ($key in @($a, $s, $w, $homeKey)) {
        [RunHoldStableSmokeInput]::KeyUp($key)
    }

    if ($runHoldProcess -and -not $runHoldProcess.HasExited) {
        Stop-Process -Id $runHoldProcess.Id -Force
    }

    if ($hadSettings) {
        [System.IO.File]::WriteAllText($settingsPath, $originalSettings)
    }
    else {
        Remove-Item -LiteralPath $settingsPath -Force -ErrorAction SilentlyContinue
    }
}
