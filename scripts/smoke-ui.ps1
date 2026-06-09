param(
    [string]$ExecutablePath = (Join-Path $PSScriptRoot '..\src\RunHold\bin\Debug\net10.0-windows\RunHold.exe')
)

$ErrorActionPreference = 'Stop'

function Wait-For {
    param(
        [scriptblock]$Probe,
        [string]$Description,
        [int]$TimeoutSeconds = 8
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $result = & $Probe
        if ($null -ne $result -and $false -ne $result) {
            return $result
        }

        Start-Sleep -Milliseconds 150
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for $Description."
}

function Find-ByName {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name
    )

    $condition = New-Object System.Windows.Automation.PropertyCondition `
        -ArgumentList ([System.Windows.Automation.AutomationElement]::NameProperty), $Name
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Find-ByNameAndControlType {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name,
        [System.Windows.Automation.ControlType]$ControlType
    )

    $nameCondition = New-Object System.Windows.Automation.PropertyCondition `
        -ArgumentList ([System.Windows.Automation.AutomationElement]::NameProperty), $Name
    $typeCondition = New-Object System.Windows.Automation.PropertyCondition `
        -ArgumentList ([System.Windows.Automation.AutomationElement]::ControlTypeProperty), $ControlType
    $condition = New-Object System.Windows.Automation.AndCondition -ArgumentList $nameCondition, $typeCondition
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Get-EditValues {
    param([System.Windows.Automation.AutomationElement]$Root)

    $condition = New-Object System.Windows.Automation.PropertyCondition `
        -ArgumentList ([System.Windows.Automation.AutomationElement]::ControlTypeProperty), ([System.Windows.Automation.ControlType]::Edit)
    $edits = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    $values = @()
    foreach ($edit in $edits) {
        $pattern = $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        $values += $pattern.Current.Value
    }

    return $values
}

function Assert-DarkWindow {
    param([System.Windows.Automation.AutomationElement]$Window)

    Add-Type -AssemblyName System.Drawing

    $rect = $Window.Current.BoundingRectangle
    $width = [Math]::Max(1, [int]$rect.Width)
    $height = [Math]::Max(1, [int]$rect.Height)
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.CopyFromScreen([int]$rect.Left, [int]$rect.Top, 0, 0, $bitmap.Size)

        $white = 0
        $samples = 0
        for ($x = 20; $x -lt ($bitmap.Width - 20); $x += 20) {
            for ($y = 100; $y -lt ($bitmap.Height - 30); $y += 20) {
                $color = $bitmap.GetPixel($x, $y)
                if ($color.R -gt 235 -and $color.G -gt 235 -and $color.B -gt 235) {
                    $white++
                }

                $samples++
            }
        }

        if ($samples -eq 0) {
            throw 'Could not sample the app window.'
        }

        $whiteRatio = $white / [double]$samples
        if ($whiteRatio -gt 0.18) {
            throw "Window still has too much white surface area: $([Math]::Round($whiteRatio * 100, 1))% of sampled pixels."
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Assert-DarkControlInterior {
    param(
        [System.Windows.Automation.AutomationElement]$Control,
        [string]$Description
    )

    Add-Type -AssemblyName System.Drawing

    $rect = $Control.Current.BoundingRectangle
    $sampleWidth = [Math]::Max(1, [int]$rect.Width)
    $sampleHeight = [Math]::Max(1, [int]$rect.Height)
    $bitmap = New-Object System.Drawing.Bitmap $sampleWidth, $sampleHeight
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.CopyFromScreen([int]$rect.Left, [int]$rect.Top, 0, 0, $bitmap.Size)
        $luminanceSamples = @()
        foreach ($xRatio in @(0.18, 0.38, 0.62, 0.82)) {
            foreach ($yRatio in @(0.35, 0.62, 0.82)) {
                $x = [Math]::Min($bitmap.Width - 1, [Math]::Max(0, [int]($bitmap.Width * $xRatio)))
                $y = [Math]::Min($bitmap.Height - 1, [Math]::Max(0, [int]($bitmap.Height * $yRatio)))
                $color = $bitmap.GetPixel($x, $y)
                $luminanceSamples += (0.2126 * $color.R) + (0.7152 * $color.G) + (0.0722 * $color.B)
            }
        }

        $ordered = @($luminanceSamples | Sort-Object)
        $medianLuminance = $ordered[[int]($ordered.Count / 2)]
        if ($medianLuminance -gt 130) {
            throw "$Description is still too bright to read in dark mode. Median luminance: $([Math]::Round($medianLuminance, 1))."
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

if (!(Test-Path -LiteralPath $ExecutablePath)) {
    throw "RunHold executable was not found at $ExecutablePath. Run the build first."
}

$existing = Get-Process -Name RunHold -ErrorAction SilentlyContinue
if ($existing) {
    throw 'Close any running RunHold process before running this smoke test.'
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms

$settingsPath = Join-Path $env:LOCALAPPDATA 'RunHold\settings.json'
$settingsFolder = Split-Path -Parent $settingsPath
$hadSettings = Test-Path -LiteralPath $settingsPath
$originalSettings = if ($hadSettings) { Get-Content -LiteralPath $settingsPath -Raw } else { $null }
$process = $null

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

    $process = Start-Process -FilePath $ExecutablePath -PassThru
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $windowCondition = New-Object System.Windows.Automation.PropertyCondition `
        -ArgumentList ([System.Windows.Automation.AutomationElement]::NameProperty), 'RunHold'
    $window = Wait-For { $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $windowCondition) } 'the RunHold window'

    Assert-DarkWindow -Window $window

    $bindingsTab = Wait-For { Find-ByName -Root $window -Name 'Bindings' } 'the Bindings tab'
    $selection = $bindingsTab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $selection.Select()
    Assert-DarkControlInterior -Control $bindingsTab -Description 'Selected Bindings tab'

    $setToggleButton = Wait-For { Find-ByName -Root $window -Name 'Set Toggle Trigger' } 'the Set Toggle Trigger button'
    $setStopButton = Find-ByName -Root $window -Name ('Set ' + 'Stop Key')
    if ($setStopButton) {
        throw 'Toggle binding smoke failed. Stop key controls were visible.'
    }

    $invoke = $setToggleButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()

    Wait-For { (Get-EditValues -Root $window) -contains 'Press key or mouse...' } 'the trigger-capture prompt' | Out-Null

    [System.Windows.Forms.SendKeys]::SendWait('a')

    Wait-For { (Get-EditValues -Root $window) -contains 'A' } 'the captured A key' | Out-Null

    'RunHold UI smoke passed: dark window, toggle-trigger binding UI, capture prompt, and key capture.'
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }

    if ($hadSettings) {
        [System.IO.File]::WriteAllText($settingsPath, $originalSettings)
    }
    else {
        Remove-Item -LiteralPath $settingsPath -Force -ErrorAction SilentlyContinue
    }
}
