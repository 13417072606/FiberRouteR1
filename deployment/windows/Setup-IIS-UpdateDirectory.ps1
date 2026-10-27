param(
    [Parameter(Mandatory=$true)][string]$SiteName,
    [Parameter(Mandatory=$true)][string]$PublicBaseUrl,
    [string]$PhysicalPath = 'D:\FiberRouteUpdates\www',
    [switch]$CreateShare,
    [string]$ShareName = 'FiberRouteUpdates',
    [string]$PublisherAccount = ''
)
$ErrorActionPreference = 'Stop'
$uri = [Uri]$PublicBaseUrl
if (!$uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.Query -or $uri.Fragment -or $uri.UserInfo) { throw '请填写真实HTTPS目录地址，例如 https://单位域名/fiber-updates/。' }
$alias = $uri.AbsolutePath.Trim('/')
if (!$alias -or $alias.Contains('/') -or $alias -notmatch '^[A-Za-z0-9_-]+$') { throw '请使用网站下的单级更新目录，例如 /fiber-updates/。' }
Import-Module WebAdministration -ErrorAction Stop
$site = Get-Website -Name $SiteName -ErrorAction Stop
if (!$site) { throw '未找到指定IIS网站，请先让网管确认站点名称。' }
if (@(Get-WebBinding -Name $SiteName -Protocol 'https').Count -eq 0) { throw '该站点尚无HTTPS绑定，请先配置域名及有效证书。本脚本不会改动现有网站证书或端口。' }
$PhysicalPath = [IO.Path]::GetFullPath($PhysicalPath)
if ($PhysicalPath -eq [IO.Path]::GetPathRoot($PhysicalPath)) { throw '请使用独立的更新目录，不要使用磁盘根目录。' }
New-Item -ItemType Directory -Path $PhysicalPath -Force | Out-Null
$config = Join-Path $PSScriptRoot 'web.config'
if (!(Test-Path -LiteralPath $config)) { throw '缺少随包提供的web.config。' }
$targetConfig = Join-Path $PhysicalPath 'web.config'
if (Test-Path -LiteralPath $targetConfig) {
    if ((Get-FileHash -LiteralPath $config).Hash -ne (Get-FileHash -LiteralPath $targetConfig).Hash) { throw '目标目录已有不同web.config，请网管复核后再操作，脚本不会覆盖。' }
} else { Copy-Item -LiteralPath $config -Destination $targetConfig }
$existing = Get-WebVirtualDirectory -Site $SiteName | Where-Object { $_.Path -eq ('/' + $alias) }
if ($existing) {
    if ([IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($existing.PhysicalPath)) -ne $PhysicalPath) { throw '同名虚拟目录已指向其他位置，脚本不会修改。' }
} else { New-WebVirtualDirectory -Site $SiteName -Name $alias -PhysicalPath $PhysicalPath | Out-Null }
Set-WebConfigurationProperty -PSPath "IIS:\" -Location ($SiteName + "/" + $alias) -Filter "system.webServer/security/authentication/anonymousAuthentication" -Name enabled -Value $true
$acl = Get-Acl -LiteralPath $PhysicalPath
$identity = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-568')
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$acl.SetAccessRule($rule)
Set-Acl -LiteralPath $PhysicalPath -AclObject $acl
if ($CreateShare) {
    if (!$PublisherAccount) { throw '创建内网共享时必须指定允许写入的PublisherAccount，不能使用Everyone。' }
    $share = Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue
    if ($share) { if ($share.Path -ne $PhysicalPath) { throw '同名共享已指向其他目录。' } }
    else { New-SmbShare -Name $ShareName -Path $PhysicalPath -ChangeAccess $PublisherAccount -FolderEnumerationMode AccessBased | Out-Null }
    $writer = New-Object System.Security.AccessControl.FileSystemAccessRule($PublisherAccount, 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($writer); Set-Acl -LiteralPath $PhysicalPath -AclObject $acl
    Write-Host ('管理方内网发布目录：\\' + $env:COMPUTERNAME + '\' + $ShareName)
}
$health = @{ Service='FiberRoute update directory'; Status='directory-ready'; Manifest='not-yet-published' } | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $PhysicalPath 'health.json'), $health, (New-Object Text.UTF8Encoding($false)))
Write-Host ('目录已建立：' + $PhysicalPath) -ForegroundColor Green
Write-Host ('请从外网验证：' + $PublicBaseUrl.TrimEnd('/') + '/health.json')
Write-Host '随后用管理方EXE生成并发布更新包；客户端配置和manifest.json均由管理方EXE生成。此步骤尚未验证公网、DNS和证书链。'
