param(
    [string]$Version = "1.0.2",
    [string]$Runtime = "win-x64",
    [bool]$BundleProjectAssets = $true,
    [string]$UnityProjectRoot = "E:\Game\CatGame\CatGameCodeOptimizationBuild"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$IssFile = Join-Path $PSScriptRoot "installer.iss"

& (Join-Path $PSScriptRoot "Publish.ps1") -Version $Version -Runtime $Runtime `
    -BundleProjectAssets $BundleProjectAssets -UnityProjectRoot $UnityProjectRoot
if ($LASTEXITCODE -ne 0) {
    throw "发布失败，退出码 $LASTEXITCODE"
}

$Candidates = @(
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$Iscc = $Candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $Iscc) {
    Write-Host ""
    Write-Host "未安装 Inno Setup 6，publish 目录已生成：$ProjectRoot\dist\publish"
    Write-Host "安装后重跑本脚本即可生成安装包：https://jrsoftware.org/isdl.php"
    exit 2
}

# Inno 自带语言包不含简体中文；缺语言包时把 build 目录里的翻译补进去。
$langDir = Join-Path (Split-Path -Parent $Iscc) "Languages"
$langFile = Join-Path $langDir "ChineseSimplified.isl"
if (-not (Test-Path $langFile)) {
    $bundledLang = Join-Path $PSScriptRoot "ChineseSimplified.isl"
    if (Test-Path $bundledLang) {
        New-Item -ItemType Directory -Path $langDir -Force | Out-Null
        Copy-Item -LiteralPath $bundledLang -Destination $langFile -Force
    }
    else {
        throw "缺少简体中文语言包：$langFile"
    }
}

Write-Host "== 生成安装包 =="
Push-Location $PSScriptRoot
try {
    & $Iscc "/DMyAppVersion=$Version" $IssFile
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup 编译失败，退出码 $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

$setup = Get-ChildItem -LiteralPath (Join-Path $ProjectRoot "dist") -Filter "MapEditorTool-Setup-*.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($setup) {
    Write-Host "安装包：$($setup.FullName)"
}
