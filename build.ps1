$CSC = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$BASE = Split-Path -Parent $MyInvocation.MyCommand.Path
$ICO = "C:\Users\Para\REPOTrainer\app.ico"
$REFS = "/reference:System.dll","/reference:System.Windows.Forms.dll","/reference:System.Drawing.dll","/reference:System.Core.dll"
$STAGE = "$BASE\_build"

if (-not (Test-Path $STAGE)) { New-Item -ItemType Directory $STAGE | Out-Null }
if (-not (Test-Path "$STAGE\plugins")) { New-Item -ItemType Directory "$STAGE\plugins" | Out-Null }

# ── Build everything to staging dir ──

Write-Host "=== Building ParasTrainerAPI.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$STAGE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\ParasTrainerAPI.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "API build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building REPOPlugin.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$STAGE\plugins\REPOPlugin.dll" /reference:"$STAGE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\plugins\REPOPlugin.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "REPO plugin build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building RoR2Plugin.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$STAGE\plugins\RoR2Plugin.dll" /reference:"$STAGE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\plugins\RoR2Plugin.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "RoR2 plugin build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building GWYFPlugin.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$STAGE\plugins\GWYFPlugin.dll" /reference:"$STAGE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\plugins\GWYFPlugin.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "GWYF plugin build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building MecchaPlugin.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$STAGE\plugins\MecchaPlugin.dll" /reference:"$STAGE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\plugins\MecchaPlugin.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "Meccha plugin build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building CatMailCoPlugin.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$STAGE\plugins\CatMailCoPlugin.dll" /reference:"$STAGE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\plugins\CatMailCoPlugin.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "CatMailCo plugin build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building PalworldPlugin.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$STAGE\plugins\PalworldPlugin.dll" /reference:"$STAGE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\plugins\PalworldPlugin.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "Palworld plugin build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building TheOneFishPlugin.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$STAGE\plugins\TheOneFishPlugin.dll" /reference:"$STAGE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\plugins\TheOneFishPlugin.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "The One Fish plugin build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building DSTPlugin.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$STAGE\plugins\DSTPlugin.dll" /reference:"$STAGE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\plugins\DSTPlugin.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "DST plugin build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building ParasTrainer.exe ===" -ForegroundColor Cyan
& $CSC /target:winexe /out:"$STAGE\ParasTrainer.exe" /win32icon:$ICO /reference:"$STAGE\ParasTrainerAPI.dll" $REFS /reference:System.Linq.dll /langversion:5 /nowarn:0168,0219,0649 "$BASE\ParasTrainer.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "Shell build failed!" -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "=== Staged build complete ===" -ForegroundColor Green

# ── Copy staged files to live dir ──
$copied = 0
$failed = 0

foreach ($f in @("ParasTrainerAPI.dll", "ParasTrainer.exe")) {
    try {
        Copy-Item "$STAGE\$f" "$BASE\$f" -Force -ErrorAction Stop
        $copied++
    } catch {
        $failed++
        Write-Host "  SKIP $f (locked by running trainer)" -ForegroundColor Yellow
    }
}

foreach ($f in (Get-ChildItem "$STAGE\plugins\*.dll")) {
    try {
        Copy-Item $f.FullName "$BASE\plugins\$($f.Name)" -Force -ErrorAction Stop
        $copied++
    } catch {
        $failed++
        Write-Host "  SKIP $($f.Name) (locked)" -ForegroundColor Yellow
    }
}

if ($failed -gt 0) {
    Write-Host ""
    Write-Host "  $copied files copied, $failed skipped (locked)" -ForegroundColor Yellow
    Write-Host "  Staged build ready at: _build\" -ForegroundColor Yellow
    Write-Host "  Restart trainer to pick up locked files" -ForegroundColor Yellow
} else {
    Write-Host "  All files deployed" -ForegroundColor Green
}

# ── Release build (single exe, all plugins embedded) ──

Write-Host ""
Write-Host "=== Building single-file release ===" -ForegroundColor Yellow

$ALL_CS = @(
    "$BASE\ParasTrainerAPI.cs",
    "$BASE\ParasTrainer.cs",
    "$BASE\plugins\RoR2Plugin.cs",
    "$BASE\plugins\REPOPlugin.cs",
    "$BASE\plugins\GWYFPlugin.cs",
    "$BASE\plugins\MecchaPlugin.cs",
    "$BASE\plugins\CatMailCoPlugin.cs",
    "$BASE\plugins\PalworldPlugin.cs",
    "$BASE\plugins\TheOneFishPlugin.cs",
    "$BASE\plugins\DSTPlugin.cs"
)

if (-not (Test-Path "$BASE\release")) { New-Item -ItemType Directory "$BASE\release" | Out-Null }

& $CSC /target:winexe /out:"$BASE\release\ParasTrainer.exe" /win32icon:$ICO $REFS /reference:System.Linq.dll /langversion:5 /nowarn:0168,0219,0649 $ALL_CS
if ($LASTEXITCODE -ne 0) { Write-Host "Release build failed!" -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "=== Release build complete ===" -ForegroundColor Green
Write-Host "  release\ParasTrainer.exe (single file, ready to distribute)"
