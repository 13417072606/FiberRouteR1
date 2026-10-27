# Windows单位服务器：在线更新部署

当前尚未收到单位外网网址和服务器访问方式，本文为可执行的部署准备，不代表服务已经上线。

优先复用单位现有HTTPS网站，在网站下建立独立的`/fiber-updates/`目录。服务器不断电之外，还必须满足：客户能访问该域名、HTTPS证书有效、域名解析及端口可达。单位网站已有HTTPS时通常无需另租云服务器；是否使用IIS、配置权限及外网访问需网管确认。

## 如果现有网站使用IIS

先由网管确认站点名称和真实HTTPS网址。在服务器管理员PowerShell中执行随包脚本，例如：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\Setup-IIS-UpdateDirectory.ps1' -SiteName '现有IIS站点名称' -PublicBaseUrl 'https://单位真实域名/fiber-updates/' -PhysicalPath 'D:\FiberRouteUpdates\www'
```

以上地址是填写格式，不能原样作为实际地址。脚本只增加更新虚拟目录，并为该目录启用匿名只读访问JSON和ZIP，不替换现有网站、不更改域名或证书、不开放新公网端口。已有同名目录、配置或绑定不符合时会停止供网管复核。

管理方可以在服务器本机运行“管理方发布工具.exe”，将服务器发布目录选为`D:\FiberRouteUpdates\www`。若在单位其他电脑操作，可以由网管建立限定发布账号权限的内网共享：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\Setup-IIS-UpdateDirectory.ps1' -SiteName '现有IIS站点名称' -PublicBaseUrl 'https://单位真实域名/fiber-updates/' -CreateShare -PublisherAccount '单位域\发布账号'
```

管理方EXE选择`\\服务器名称\FiberRouteUpdates`作为发布目录。共享只用于单位内网管理，不能把SMB端口映射到公网。客户仅通过HTTPS读取签名清单和版本ZIP。

## 管理方EXE的四步

1. 生成更新包：选择真实数据库目录或新版本完整软件目录、填写版本和说明。数据源先在主程序中完成导入/复核，打包时关闭主程序。
2. 配置服务：填写管理员本地工作目录、单位真实HTTPS更新地址、服务器发布目录。首次初始化生成管理员签名密钥；客户配置只有公钥与地址。
3. 生成发布清单：选择更新包生成签名清单。软件与数据独立，保留另一个渠道。相同版本不能替换不同内容。
4. 发布与验证：复制版本ZIP并核对哈希，最后替换manifest.json。再点“验证外网清单”。

客户配置`update-service.json`放到客户运行目录的“资源”文件夹，或在管理方工具中点“应用到本机客户端”。客户设置页分别检测软件和数据更新；数据先预览差异，软件退出后升级重启。

只把EXE产生的site内容发布到网站。签名私钥、管理工作目录、原始Excel、原始personal.db不放进网站目录。

## 验收顺序

- 单位外网电脑打开`https://真实域名/fiber-updates/health.json`。
- 发布一次测试版本后，管理方EXE“验证外网清单”成功。
- 一台客户测试机安装客户配置，检测到两个渠道各自版本，下载校验并应用。
- 核对人工记录、去重/无变化忽略和失败回退，再正式发给所有客户。

## 如果现有网站不是IIS

无需强行安装IIS或改动现有网站。由网管把同一个独立目录映射到现有HTTPS站点，允许静态读取JSON与ZIP、禁用目录列表，并关闭manifest.json长期缓存。管理方EXE仍通过本机/内网共享目录发布，客户端协议不变。

## 云盘与免费方案

普通网盘的提取码、登录、重定向、下载限速/限额容易影响自动更新；不作为默认更新源。免费静态托管或对象存储适合试验，需核对国内直连、流量额度、单文件大小及稳定性，不能承诺长期免费。单位现有网站可达时，优先复用，通常成本最低。

部署配置将更新目录限定为GET/HEAD静态读取JSON和ZIP，避开现有网站动态应用处理器。部署脚本需要IIS静态内容组件；若现有服务器没有IIS或不是IIS站点，请由网管使用既有Web服务映射目录，脚本不会自动安装或替换服务器软件。图形工具需支持.NET 8及桌面环境的Windows版本。
