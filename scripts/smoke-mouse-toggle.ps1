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

public static class RunHoldMouseToggleSmokeInput
{
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint MouseEventFXDown = 0x0080;
    private const uint MouseEventFXUp = 0x0100;
    private const uint XButton1 = 0x0001;

    public static void KeyDown(byte virtualKey)
    {
        keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
    }

    public static void KeyUp(byte virtualKey)
    {
        keybd_event(virtualKey, 0, KeyEventFKeyUp, UIntPtr.Zero);
    }

    public static void MouseButton4Down()
    {
        mouse_event(MouseEventFXDown, 0, 0, XButton1, UIntPtr.Zero);
    }

    public static void MouseButton4Up()
    {
        mouse_event(MouseEventFXUp, 0, 0, XButton1, UIntPtr.Zero);
    }

    public static bool IsDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

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

try {
    New-Item -ItemType Directory -Path $settingsFolder -Force | Out-Null
    $testSettings = @{
        ToggleBinding = @{ Device = 1; Code = 1; DisplayName = 'Mouse Button 4' }
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

    [RunHoldMouseToggleSmokeInput]::KeyDown($a)
    [RunHoldMouseToggleSmokeInput]::KeyDown($s)
    [RunHoldMouseToggleSmokeInput]::KeyDown($w)
    Start-Sleep -Milliseconds 150
    [RunHoldMouseToggleSmokeInput]::MouseButton4Down()
    Start-Sleep -Milliseconds 40
    [RunHoldMouseToggleSmokeInput]::MouseButton4Up()
    Start-Sleep -Milliseconds 100
    [RunHoldMouseToggleSmokeInput]::KeyUp($a)
    [RunHoldMouseToggleSmokeInput]::KeyUp($s)
    [RunHoldMouseToggleSmokeInput]::KeyUp($w)
    Start-Sleep -Milliseconds 150

    $samples = 0
    $allDownSamples = 0
    for ($i = 0; $i -lt 25; $i++) {
        $allDown = [RunHoldMouseToggleSmokeInput]::IsDown($a) -and [RunHoldMouseToggleSmokeInput]::IsDown($s) -and [RunHoldMouseToggleSmokeInput]::IsDown($w)
        if ($allDown) {
            $allDownSamples++
        }

        $samples++
        Start-Sleep -Milliseconds 20
    }

    if ($allDownSamples -lt 22) {
        throw "Mouse toggle smoke failed. Expected A/S/W to stay down after physical release; saw all three down in $allDownSamples of $samples samples."
    }

    [RunHoldMouseToggleSmokeInput]::MouseButton4Down()
    Start-Sleep -Milliseconds 40
    [RunHoldMouseToggleSmokeInput]::MouseButton4Up()
    Start-Sleep -Milliseconds 200

    $anyStillDown = [RunHoldMouseToggleSmokeInput]::IsDown($a) -or [RunHoldMouseToggleSmokeInput]::IsDown($s) -or [RunHoldMouseToggleSmokeInput]::IsDown($w)
    if ($anyStillDown) {
        throw 'Mouse toggle smoke failed. At least one held key was still down after Mouse Button 4 stop.'
    }

    'RunHold mouse-toggle smoke passed: Mouse Button 4 held A/S/W after physical release and stopped them.'
}
finally {
    [Environment]::SetEnvironmentVariable('RUNHOLD_ACCEPT_EXTERNAL_INJECTED_INPUT_FOR_SMOKE', $previousSmokeFlag, 'Process')

    foreach ($key in @($a, $s, $w)) {
        [RunHoldMouseToggleSmokeInput]::KeyUp($key)
    }
    [RunHoldMouseToggleSmokeInput]::MouseButton4Up()

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
