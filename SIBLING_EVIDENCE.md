# 兄弟项目与本地 API 证据

查阅对象：`C:\Users\Payne\OneDrive\文档\Visual Studio 2022\Projects\S3D_PaintingImporter`，仅读取，未修改。业务规则仍以本项目 [SPEC.md](SPEC.md) 为准。

| 事项 | 找到的证据 | 对本工具的结论 |
|---|---|---|
| 目标框架 | `S3D_PaintingImporter.csproj:4` 使用 `net462`；本项目负责人也已确认 | 本解决方案全部目标框架改为 `net462` |
| S3D 命令入口 | `ImporterCommand.cs:13,18,31-33` 使用 `BaseModalCommand`、活动 Site/Plant、WinForms 对话框 | 可参考宿主接入形式；该项目版本注释称 S3D 2018，不能据此确认本项目 S3D 版本 |
| 涂装属性候选 | `PaintingImporter.cs:243-245` 写 `IJCoatingInfo.CoatingRequirement` 和 `IJCoatingInfo.CoatingType`；`user_manual.md:24-25` 将其标为 CodeList | 只确认这两个属性在兄弟工具中被用于涂装导入；尚不能确认 Paint Code 应写哪一个，也没有本项目代码到 CodeList ID 的映射 |
| CodeList 写法 | `PaintingImporter.cs:301-319,341-345` 检查属性类型、解析整数 ID、调用 `SetPropertyValue` 并读回 | 本工具必须先验证目标 CodeList 中存在所选 Paint Code，再在提交后读回确认；不能直接把代码文本当作有效 ID |
| 事务 | `PaintingImporter.cs:113,149,164` 使用 `MiddleServiceProvider.TransactionMgr`，成功时 Commit、异常时 Abort | 可参考 API 位置；兄弟项目遇到逐行错误后仍可能提交成功行，本项目按 SPEC 要求采用整批原子事务，不照搬其错误处理 |
| 线程 | `MainForm.cs:131,143-145` 只把 Excel 读取放在 `Task.Run`，S3D 对象访问留在主 STA 线程 | Phase 2/3 的读取和写入在宿主线程进行；报告生成仍可在后台线程运行 |
| 对象定位 | `PaintingImporter.cs:175-231` 以对象名称查找，并有模糊回退 | 本项目写入应基于评估时的 PipeRun OID/对象引用，不采用名称模糊回退 |

本地 Smart 3D RAG 另找到 Hexagon 的 *TSMP4003 - Smart3D NET API - Course Guide.pdf*，示例通过 `IJRtePipeRun` 检查选中对象，并从 `IJProcessDataInfo.OperatingMaxTemp` 读取 PipeRun 最高操作温度。它支持 Phase 2 属性调查方向，但尚未验证该项目模型中的接口配置、单位和其余温度字段，暂不写入适配器常量。

兄弟项目没有提供 Spec→材质映射、H3/PP 实际值、Underground 标识、C093U/TSA 等选择依据，或 Paint Code 的最终目标属性。这些事项继续保持 `OWNER-TBD`。

## S3D_ShortCodeExporter（2026-10-15 补充查阅）

查阅对象：`C:\Users\Payne\OneDrive\文档\Visual Studio 2022\Projects\S3D_ShortCodeExporter`，仅读取，未修改。

| 事项 | 找到的证据 | 对本工具的结论 |
|---|---|---|
| 框架与宿主位数 | `src/S3D_ShortCodeExporter/S3D_ShortCodeExporter.csproj:4-7` 为 `net462`、`x86` | 再次印证目标框架；x86 是该兄弟项目的部署配置，本项目接入 S3D 前须确认目标宿主进程位数 |
| 命令入口和依赖 | 同一项目文件 `:13-17` 在存在 S3D DLL 时定义编译符号；`Commands/ShortCodeExporterCommand.cs:14-30` 使用 `BaseModalCommand` | 可参考按本机 S3D DLL 配置宿主命令；当前 Phase 1 不引入这些依赖 |
| 属性读取 | `Services/Smart3DPropertyAdapters.cs:26-57` 检查 `SupportsInterface`，调用 `GetPropertyValue` 并读取 `PropValue` | 可参考 Phase 2 的属性读取封装和缺失值处理；其对象是目录件，不证明 PipeRun 的属性名 |
| Catalog Spec 来源 | `README.md:50-58` 明确只读取 `PlantCatalog`/CDB，通过目录关系取得 Spec、ShortCode 和材料描述，不读取 PlantModel occurrence | 目录 Spec 不等于模型 PipeRun.Spec；不能拿目录部件材料推断 SpecMaterialMapping，也不能用该项目导出的 Spec 清单代替模型中实际使用的 Spec 清单 |
| 大批量报告 | `README.md:68-76` 和 `Services/StreamingWorkbookWriter.cs:14,40-77,99-109` 使用流式 XLSX、临时分桶、超行数拆 Sheet，考虑 32 位宿主内存 | Phase 2 扫描全模型前应估算规模；如数据量可能较大，报告改为流式写入和跨 Sheet 拆分，不能静默截断。当前 Phase 1 报告仍一次保留全部结果，仅适合已知规模的演示输入 |

该项目没有 PipeRun 的 Spec、操作温度、保温、Underground 或 Paint Code 的模型属性映射，也没有本项目的材质分类和涂漆选择规则。因此现有 `OWNER-TBD` 不变。
