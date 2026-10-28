param(
    [ValidateSet('Prepare','Verify','Inspect')][string]$Stage = 'Prepare',
    [string]$RuntimeDirectory = '',
    [string]$WorkspaceDirectory = 'F:\管线资源发布区',
    [string]$DataDirectory = '',
    [string]$DataVersion = ''
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$OutputEncoding = [Text.UTF8Encoding]::new($false)
try { [Console]::OutputEncoding = $OutputEncoding } catch {}

function Select-Directory([string]$Description, [string]$Initial) {
    if ($env:OS -ne 'Windows_NT') { throw '请通过参数指定目录。' }
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = $Description
    if ($Initial -and (Test-Path -LiteralPath $Initial -PathType Container)) { $dialog.SelectedPath = $Initial }
    try {
        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { throw '已取消，未继续发布。' }
        return $dialog.SelectedPath
    } finally { $dialog.Dispose() }
}
if (!$RuntimeDirectory) { $RuntimeDirectory = Select-Directory '选择原软件运行目录，里面应有程序和数据文件夹' '' }
$RuntimeDirectory = [IO.Path]::GetFullPath($RuntimeDirectory)
$program = Join-Path $RuntimeDirectory '程序'
$assembly = Join-Path $program 'FiberRouteR1.dll'
if (!(Test-Path -LiteralPath $assembly -PathType Leaf)) { throw '所选目录缺少程序\FiberRouteR1.dll，请选择原来的软件运行目录。' }
$programVersion = [Reflection.AssemblyName]::GetAssemblyName($assembly).Version
if ($programVersion -lt [Version]'1.4.3') { throw '请先手动安装 R1.4.3 或 R1.4.4，再设置分片在线更新。' }
if (!(Test-Path -LiteralPath $WorkspaceDirectory -PathType Container)) { $WorkspaceDirectory = Select-Directory '选择原管理工作目录（含 private-key.pem），不要创建新密钥' '' }
$WorkspaceDirectory = [IO.Path]::GetFullPath($WorkspaceDirectory)
$configFile = Join-Path $WorkspaceDirectory 'update-service.json'
if (!(Test-Path -LiteralPath $configFile -PathType Leaf)) { throw '原工作目录缺少服务配置。请先在管理方发布工具第2步选择原工作目录并保存服务配置。' }
$config = Get-Content -LiteralPath $configFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ([Uri]$config.ManifestUrl -ne [Uri]'https://guangludiaodu.pages.dev/manifest.json') { throw '当前配置不是原 guangludiaodu.pages.dev 项目，请核对第2步客户HTTPS地址。' }
if ($Stage -eq 'Prepare') {
    if (Get-Process -Name FiberRouteR1,FiberRoute.Publisher -ErrorAction SilentlyContinue) { throw '请先关闭主程序和管理方发布工具，再重新运行。' }
    if (!$DataDirectory) {
        $location = Join-Path $env:LOCALAPPDATA 'FiberRouteR1\data-location.txt'
        if (Test-Path -LiteralPath $location -PathType Leaf) { $DataDirectory = (Get-Content -LiteralPath $location -Raw).Trim() }
        else { $DataDirectory = Select-Directory '选择主程序设置中显示的当前数据目录，里面应有 resources.db 和 personal.db' (Join-Path $RuntimeDirectory '数据') }
    }
    if (!$DataVersion) { $china = [TimeZoneInfo]::FindSystemTimeZoneById('China Standard Time'); $DataVersion = [TimeZoneInfo]::ConvertTimeFromUtc([DateTime]::UtcNow,$china).ToString('yyyy-MM-dd.HHmmss') }
}
# Copy only this small auxiliary executable; reuse the existing .NET runtime.
$helperFiles = @('FiberRoute.Online.exe','FiberRoute.Online.dll','FiberRoute.Online.deps.json','FiberRoute.Online.runtimeconfig.json')
foreach ($name in $helperFiles) {
    $file = Join-Path (Join-Path $PSScriptRoot '工具') $name
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw '辅助工具不完整，请完整解压在线发布辅助包。' }
    Copy-Item -LiteralPath $file -Destination (Join-Path $program $name) -Force
}
$helper = Join-Path $program 'FiberRoute.Online.exe'
switch ($Stage) {
    'Prepare' { & $helper prepare $WorkspaceDirectory $RuntimeDirectory $DataDirectory $DataVersion }
    'Verify' { & $helper verify $WorkspaceDirectory }
    'Inspect' { & $helper inspect $WorkspaceDirectory }
}
if ($LASTEXITCODE -ne 0) { throw '在线发布步骤未完成，请把上面的具体错误发回核对。' }
