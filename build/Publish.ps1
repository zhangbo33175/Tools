param(
    [string]$Version = "1.0.0",
    [string]$Runtime = "win-x64",
    [bool]$BundleProjectAssets = $true,
    [string]$UnityProjectRoot = "E:\Game\CatGame\CatGameCodeOptimizationBuild"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$ProjectFile = Join-Path $ProjectRoot "MapEditorTool.csproj"
$PublishDir = Join-Path $ProjectRoot "dist\publish"
$SeedConfig = Join-Path $ProjectRoot "bin\Debug\net10.0\output\Config"

Write-Host "== 发布 MapEditorTool $Version ($Runtime) =="
Write-Host "项目：$ProjectFile"
Write-Host "输出：$PublishDir"

if (Test-Path $PublishDir) {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force
}

& dotnet publish $ProjectFile `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -p:FileVersion="$Version.0" `
    -p:InformationalVersion=$Version `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败，退出码 $LASTEXITCODE"
}

# 编辑器通过 AppContext.BaseDirectory 找项目根，运行时会读 MapData / docs / output。
# 这三个目录必须和 exe 同目录，否则安装后打开是空地图、没有图片和模板。
Write-Host "== 复制运行数据 =="
foreach ($folder in @("MapData", "docs")) {
    $source = Join-Path $ProjectRoot $folder
    if (-not (Test-Path $source)) {
        throw "缺少数据目录：$source"
    }

    Copy-Item -LiteralPath $source -Destination $PublishDir -Recurse -Force
    Write-Host "  + $folder"
}

# output\Config 是首次运行的模板 / Spine / 动画配置种子；已有用户配置不要覆盖。
$targetConfig = Join-Path $PublishDir "output\Config"
if (Test-Path $SeedConfig) {
    if (-not (Test-Path $targetConfig)) {
        New-Item -ItemType Directory -Path $targetConfig -Force | Out-Null
    }

    Copy-Item -Path (Join-Path $SeedConfig "*") -Destination $targetConfig -Recurse -Force
    Write-Host "  + output\Config（含本机资源根/表格路径，换机器需在编辑器里重设）"
}
else {
    Write-Warning "未找到 output\Config 种子，安装包首次启动会生成默认配置。"
}

# 发布目录不需要符号文件；SkiaSharp / HarfBuzz 的原生 pdb 会额外占约 100 MB。
Get-ChildItem -LiteralPath $PublishDir -Recurse -File -Filter *.pdb |
    Remove-Item -Force

if ($BundleProjectAssets) {
    Write-Host "== 打包 Unity 工程资源（离线安装） =="
    if (-not (Test-Path $UnityProjectRoot)) {
        throw "缺少 Unity 工程根：$UnityProjectRoot"
    }

    $resSource = Join-Path $UnityProjectRoot "Assets\Res"
    if (-not (Test-Path $resSource)) {
        throw "缺少资源目录：$resSource"
    }

    $resTarget = Join-Path $PublishDir "Assets\Res"
    New-Item -ItemType Directory -Path $resTarget -Force | Out-Null
    Copy-Item -Path (Join-Path $resSource "*") -Destination $resTarget -Recurse -Force
    Write-Host "  + Assets\Res"

    # 章节 / 关卡 / 元素表是编辑器左侧章节树与物件模板的来源。
    $tableRoot = Join-Path $UnityProjectRoot "Docs\Designs\Excels\Tables\AlTables"
    $levelSource = Join-Path $tableRoot "LevelConfigs"
    $itemSource = Join-Path $tableRoot "Item"
    $tableTarget = Join-Path $PublishDir "Docs\Designs\Excels\Tables\AlTables"
    New-Item -ItemType Directory -Path (Join-Path $tableTarget "LevelConfigs") -Force | Out-Null
    foreach ($name in @("TableChapters.xlsm", "TableMainLevels.xlsm")) {
        $file = Join-Path $levelSource $name
        if (-not (Test-Path $file)) {
            throw "缺少配置表：$file"
        }
        Copy-Item -LiteralPath $file -Destination (Join-Path $tableTarget "LevelConfigs") -Force
    }
    if (Test-Path $itemSource) {
        Copy-Item -LiteralPath $itemSource -Destination (Join-Path $tableTarget "Item") -Recurse -Force
    }
    Write-Host "  + Docs\...\LevelConfigs / Item"

    # 改成安装目录相对路径：project=. 表示以 exe 所在目录为工程根，
    # 资源在 <安装目录>\Assets\Res，表格在 <安装目录>\Docs\...
    $targetConfig = Join-Path $PublishDir "output\Config"
    New-Item -ItemType Directory -Path $targetConfig -Force | Out-Null
    $roots = "# 安装目录相对资源根`r`nproject=.`r`n"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Join-Path $targetConfig "resRoots.txt"), $roots, $utf8NoBom)

    $settingsPath = Join-Path $targetConfig "EditorSettings.json"
    if (Test-Path $settingsPath) {
        $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    }
    else {
        $settings = [pscustomobject]@{
            TableChaptersPath = "../Docs/Designs/Excels/Tables/AlTables/LevelConfigs/TableChapters.xlsm"
            TableMainLevelsPath = "../Docs/Designs/Excels/Tables/AlTables/LevelConfigs/TableMainLevels.xlsm"
            ItemElementTablesDirectory = "../Docs/Designs/Excels/Tables/AlTables/Item"
            ItemElementCsvPath = "../Docs/Designs/Excels/Tables/AlTables/Item/ItemElementConfig.csv"
            ObjectPreviewPrefabsDirectory = "Assets/Res/Prefabs/Map/Objects"
            ObjectPreviewTexturesDirectory = "Assets/Res/Textures/Map/Objects"
            LevelBackgroundDirectory = "Assets/Res/Textures/Map/LevelBackground"
            BaselineStep = 10
        }
    }
    $settings.MapSaveDirectory = "../MapData/Map"
    $settingsJson = $settings | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($settingsPath, $settingsJson, $utf8NoBom)
    Write-Host "  + output\Config（已改为安装目录相对路径）"
}

$size = (Get-ChildItem -LiteralPath $PublishDir -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ("== 完成：{0:N1} MB ==" -f ($size / 1MB))
Write-Host "可执行文件：$(Join-Path $PublishDir 'MapEditorTool.exe')"
