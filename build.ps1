$CSC = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$BASE = Split-Path -Parent $MyInvocation.MyCommand.Path
$ICO = "C:\Users\Para\REPOTrainer\app.ico"
$REFS = "/reference:System.dll","/reference:System.Windows.Forms.dll","/reference:System.Drawing.dll","/reference:System.Core.dll"

# ── Dev build (separate DLLs, supports hot-reload) ──

Write-Host "=== Building ParasTrainerAPI.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$BASE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\ParasTrainerAPI.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "API build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building REPOPlugin.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$BASE\plugins\REPOPlugin.dll" /reference:"$BASE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\plugins\REPOPlugin.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "REPO plugin build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building RoR2Plugin.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$BASE\plugins\RoR2Plugin.dll" /reference:"$BASE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\plugins\RoR2Plugin.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "RoR2 plugin build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building GWYFPlugin.dll ===" -ForegroundColor Cyan
& $CSC /target:library /out:"$BASE\plugins\GWYFPlugin.dll" /reference:"$BASE\ParasTrainerAPI.dll" $REFS /langversion:5 /nowarn:0168,0219,0649 "$BASE\plugins\GWYFPlugin.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "GWYF plugin build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building ParasTrainer.exe ===" -ForegroundColor Cyan
& $CSC /target:winexe /out:"$BASE\ParasTrainer.exe" /win32icon:$ICO /reference:"$BASE\ParasTrainerAPI.dll" $REFS /reference:System.Linq.dll /langversion:5 /nowarn:0168,0219,0649 "$BASE\ParasTrainer.cs"
if ($LASTEXITCODE -ne 0) { Write-Host "Shell build failed!" -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "=== Dev build complete ===" -ForegroundColor Green
Write-Host "  ParasTrainer.exe + ParasTrainerAPI.dll + plugins\"

# ── Release build (single exe, all plugins embedded) ──

Write-Host ""
Write-Host "=== Building single-file release ===" -ForegroundColor Yellow

$ALL_CS = @(
    "$BASE\ParasTrainerAPI.cs",
    "$BASE\ParasTrainer.cs",
    "$BASE\plugins\RoR2Plugin.cs",
    "$BASE\plugins\REPOPlugin.cs",
    "$BASE\plugins\GWYFPlugin.cs"
)

if (-not (Test-Path "$BASE\release")) { New-Item -ItemType Directory "$BASE\release" | Out-Null }

& $CSC /target:winexe /out:"$BASE\release\ParasTrainer.exe" /win32icon:$ICO $REFS /reference:System.Linq.dll /langversion:5 /nowarn:0168,0219,0649 $ALL_CS
if ($LASTEXITCODE -ne 0) { Write-Host "Release build failed!" -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "=== Release build complete ===" -ForegroundColor Green
Write-Host "  release\ParasTrainer.exe (single file, ready to distribute)"
