# SealSign（电子印章签署服务 · 简化版）

目标：**文件防篡改 + 盖章有迹可查**，不需要法律效力的 CA 证书体系。
技术栈：.NET 10 + MySQL 8 + EF Core（Pomelo）。

> 版本说明：Pomelo 官方尚未发布支持 EF Core 10 的版本（最新 9.0.0），
> 因此 EF Core 相关包暂用 9.0.21（运行在 .NET 10 上无问题），其余包均为 .NET 10 版本。
> 待 Pomelo 发布 10.0.0 后直接升级 `Pomelo.EntityFrameworkCore.MySql` 与 `Microsoft.EntityFrameworkCore` 即可。

## 信任模型

| 威胁 | 对策 |
|---|---|
| 盖章后 PDF 被修改 | 盖章瞬间对最终文件算 SHA256 存档；验真时重算比对，改一个字节即检出 |
| DB 里偷改签署记录 | 每条记录含 `PrevHash` 指向上一条，形成哈希链；改一条则断链 |
| 整个库被替换/伪造记录 | 每条记录的链哈希经**平台 RSA 私钥**签名，无私钥伪造不了 |
| 库被整体回滚到旧状态 | 建议每天把最新 `ChainHash` 备份到异地（见"生产 checklist"） |
| 印章图被抠走冒用 | 盖章时自动渲染编号/签署人/时间注记 + 二维码验真链接 |

以后如需法律效力：把平台签名换成 CA 证书签名，并接入时间戳/司法链，表结构无需大改。

## 快速开始

```bash
# 1. 起 MySQL
docker compose up -d

# 2. 跑 API（首次启动自动建表；平台密钥对首次运行时自动生成）
cd src/SealSign.Api
dotnet run
# OpenAPI: http://localhost:5000/openapi/v1.json
```

`appsettings.json` 关键配置：

| 项 | 说明 |
|---|---|
| `ConnectionStrings:Default` | MySQL 连接串 |
| `Jwt:Key` | 至少 32 字符，生产必须换 |
| `Crypto:MasterKey` | 32 字节 Base64；为空则用环境变量 `SEALSIGN_MASTER_KEY`，再为空则开发模式自动生成落盘（`data/keys/master.key`，仅限开发） |
| `Storage:RootPath` | 文件存储根目录（MVP 本地磁盘） |
| `Pdf:CjkFontPath` | 中文字体 TTF 路径（可选，不配则中文注记显示方框，编号/时间不受影响） |
| `App:PublicBaseUrl` | 对外地址，用于生成二维码验真链接 |

## API 一览

| 方法 | 路径 | 说明 | 鉴权 |
|---|---|---|---|
| POST | /api/auth/register | 注册 | 否 |
| POST | /api/auth/login | 登录拿 JWT | 否 |
| POST | /api/seals | 上传印章图（png/jpg） | 是 |
| GET | /api/seals | 我的印章列表 | 是 |
| PATCH | /api/seals/{id}/disable | 停用印章 | 是 |
| POST | /api/documents/upload | 上传 PDF | 是 |
| GET | /api/documents | 我的文档 | 是 |
| GET | /api/documents/{id}/download | 下载（最新盖章版） | 是 |
| POST | /api/sign/stamp | 盖章并生成签署记录 | 是 |
| GET | /api/sign/records | 我的签署记录 | 是 |
| GET | /api/sign/records/{recordNo} | 记录详情 | 是 |
| GET | /api/sign/records/{recordNo}/download | 下载盖章版 PDF | 是 |
| POST | /api/verify/file | **验文件**：上传 PDF 验真 | 否 |
| GET | /api/verify/{recordNo} | 凭编号查记录（扫码落地） | 否 |
| POST | /api/verify/chain | 全链巡检 | 否 |

盖章坐标：`placements: [{page, xMm, yMm, widthMm, heightMm?}]`，毫米、从页面左上角起算。

## 测试

```bash
dotnet test
```

覆盖：哈希链防篡改、平台密钥加解密/签名验签/持久化、PDF 盖章渲染。

## 生产 checklist

- [ ] `Jwt:Key`、`Crypto:MasterKey` 换成高强度随机值，走环境变量/密钥管理注入
- [ ] 私钥进 HSM / 云 KMS（替换 `PlatformKeyStore` + `RsaPlatformSigner`）
- [ ] `IFileStorage` 换 MinIO / S3 / OSS 实现
- [ ] 链头哈希每日异地备份（定时任务读最新 `ChainHash` 发邮件/写异地）
- [ ] 并发：`SigningService` 的单机信号量换 Redis 分布式锁（多实例部署时）
- [ ] 验真接口加限流（公开接口防滥用）
- [ ] 反向代理配 HTTPS；MySQL 开 TLS、定期备份

## 已知限制

- PdfSharpCore 的 Modify 模式打不开极少数特殊 PDF（加密/非标准结构），上传时会直接返回解析失败
- 未配置中文字体时，印章下方中文注记显示为方框；编号与时间不受影响
- UE 等场景的骑缝章、多人会签流程为 P2 功能，当前为单人单次盖章
