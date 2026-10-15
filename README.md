# S3D Auto Paint Tool

目标：作为 Smart 3D 自定义命令运行，按 [SPEC.md](SPEC.md) 判定 PipeRun 并在满足唯一规则时写入 `IJCoatingInfo.CoatingType`。当前实现只扫描活动 Working Set 的 PipeRun，不保证覆盖整个项目。目标框架为 .NET Framework 4.6.2。

## 当前实现

- `YYB.AutoPaintCommand` 在 S3D 命令线程通过 `WorkingSet.GetObjectsByFilter` 扫描活动 Working Set 中的 `Piping\PipingRuns`，读取 Spec、操作温度、保温用途、现有 CoatingType，生成 Excel 报告。S3D 原始温度按 K 转为 °C。
- `DryRun=true` 时只评估和输出报告。改为 `false` 时，先输出预览报告，再提示将修改的条数；确认后在目标 Plant 验证每个 Paint Code 的 CodeList，并以单个事务写回。异常时 Abort，报告标记 `ERROR`。
- `REVIEW`、`MISMATCH`、`NOT_APPLICABLE` 不写入。已有涂漆代码默认不覆盖。
- 运行时弹出“另存为”窗口选择 Excel 报告位置，默认打开用户“文档”目录；取消选择则退出，不扫描或写入模型。写入预览报告保存在所选目录。配置从宿主 DLL 旁的 `config` 目录读取。

## 编译与接入

```powershell
dotnet build S3DPaintTool.sln
dotnet run --project PaintRules.Tests
```

`S3DHost` 是内部项目名，输出程序集为 `S3D_Auto_Paint_Tool.dll`。项目引用本机安装的 Smart 3D 托管程序集；可用 `-p:S3DReferencePath=...` 指定目录。编译后将 `S3D_Auto_Paint_Tool.dll`、`PaintRules.dll`、`Reporting.dll`、`S3DAdapter.dll` 和整个 `config` 目录放在同一插件目录；命令从 DLL 所在目录读取配置，不使用 S3D 安装目录。在 S3D Custom Command 配置中使用程序集名 `S3D_Auto_Paint_Tool`，命令类全名/ProgID 为 `YYB.AutoPaintCommand`。更换 DLL 后需重新启动 S3D 才能加载新程序集。命令入口已由用户在 S3D 中启动；模型扫描和属性写回仍需现场验证。

## 上线前必须补齐的模型信息

工具按项目负责人 2026-10-15 的决定，不读取或判断地上/地下。`IJRteInsulation.Purpose/Thickness` 和操作温度接口仍需用目标 Plant 进行只读验证；读取失败将落入 `REVIEW`。

`config/SpecMaterialMapping.csv` 目前只有 SPEC 提供的示例 Spec；需补充项目完整映射。`UseLowestApplicableBand=false` 与重叠温度段使多数 CS 行进入 `REVIEW`。H3、PP 与特殊服务条件也需在真实模型确认。只有这些数据补齐并在目标 Plant 做 DryRun 核对后，才能把 `DryRun` 设为 `false` 进行自动写回。

无 S3D 的规则/报告演示仍可执行：

```powershell
& .\App\bin\Debug\net462\App.exe samples/piperuns.csv report.xlsx
```
