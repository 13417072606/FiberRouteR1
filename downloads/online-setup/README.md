# 在线发布辅助工具

[操作说明](../../docs/online-setup/使用说明.md) · [验证记录](../../docs/online-setup/验证记录.md) · [SHA256](SHA256.json)

关闭主程序和管理方工具，运行 `Start-Online-Update.ps1`，选择原运行目录，复用原签名工作目录，生成可上传现有 Cloudflare Pages 项目的完整发布 ZIP。

上传后重新运行 `Start-Online-Update.ps1 -Stage Verify`，使用客户端相同逻辑下载并校验两个渠道。此工具不会替用户执行 Cloudflare 上传，也不会自动应用客户的数据或软件更新。
