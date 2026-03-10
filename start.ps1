# Usage:
#   .\start.ps1                          # sequential benchmark
#   .\start.ps1 -par 4                   # parallel benchmark, 4 threads (2|4|8|12|16)
#   .\start.ps1 -Mode image              # sequential image viewer
#   .\start.ps1 -Mode image -par 4       # parallel image viewer
#   .\start.ps1 -Mode all                # benchmark + image (sequential)
#   .\start.ps1 -Mode all -par 4         # benchmark + image (parallel)
#   .\start.ps1 -cmd                     # sequential benchmark, console output only (no image)
#   .\start.ps1 -par 4 -cmd              # parallel benchmark, console output only (no image)

param(
    [ValidateSet("benchmark", "image", "all")]
    [string]$Mode = "benchmark",
    [switch]$par,
    [ValidateSet(2, 4, 8, 12, 16)]
    [int]$Threads = 2,
    [switch]$cmd
)

Write-Host "Building Release..."
dotnet build -c Release --nologo -v q

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

$exe = "$PSScriptRoot\bin\Release\net10.0-windows\3-volitelna.exe"
$extra = if ($cmd) { "--cmd" } else { $null }

switch ($Mode) {
    "benchmark" {
        if ($par) {
            Write-Host "Running PARALLEL benchmark ($Threads threads)..."
            & $exe --benchmark-par $Threads $extra
        } else {
            Write-Host "Running SEQUENTIAL benchmark..."
            & $exe --benchmark $extra
        }
    }
    "image" {
        if ($par) {
            Write-Host "Showing PARALLEL image ($Threads threads)..."
            & $exe --image-par $Threads
        } else {
            Write-Host "Showing SEQUENTIAL image..."
            & $exe --image
        }
    }
    "all" {
        if ($par) {
            Write-Host "Running PARALLEL benchmark + image ($Threads threads)..."
            & $exe --benchmark-par $Threads $extra
            & $exe --image-par $Threads
        } else {
            Write-Host "Running SEQUENTIAL benchmark + image..."
            & $exe $extra
        }
    }
}
