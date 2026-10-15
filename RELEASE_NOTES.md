# v0.1.0-preview.1

Smart 3D Auto Paint Tool 的首个预览版。程序集目标框架为 .NET Framework 4.6.2，S3D 命令程序集名为 `S3D_Auto_Paint_Tool`，命令类为 `YYB.AutoPaintCommand`。

本版包含活动 Working Set 的 PipeRun 扫描、按项目规则判定、用户指定 XLSX 报告路径、写入前预览报告、CodeList 校验，以及确认后的单事务 `IJCoatingInfo.CoatingType` 写回。默认 `DryRun=true`，不会修改模型。原始温度按 K 转为 °C。规则表已将 `C093U-3` 统一为 `C093U3`。

这是预览版：埋地标识接口、项目完整 Spec 映射、H3/PP 实际 CodeList、重叠温度档与多候选选码规则尚需在目标 Plant 确认。当前扫描范围是活动 Working Set，不保证覆盖整个项目。S3D 命令入口已启动过，模型扫描与写回尚未完成现场验证。

部署时将 ZIP 中的 DLL 和整个 `config` 目录一起放在 S3D 自定义命令目录。运行前保持 `DryRun=true`，先核对报告与字段映射。
