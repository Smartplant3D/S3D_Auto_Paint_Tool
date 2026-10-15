# Smart3D RAG 查询结果（2026-10-15）

查询使用本机 `D:\Smart3D-RAG`，模型 `bge-m3`。下列均为 API 实现线索，不替代本项目 [SPEC.md](SPEC.md) 的业务规则，也不证明当前 Plant 已配置相应属性或 CodeList 值。

| 主题 | RAG 来源与发现 | 当前判断 |
|---|---|---|
| PipeRun 识别与范围 | Hexagon *TSMP4003 - Smart3D NET API - Course Guide.pdf* 的 Generic Property Access Command 示例用 `SupportsInterface("IJRtePipeRun")` 验证选择对象；项目样例 `S3D_Feature_Moved/PipeRunMover.cs:200` 的 Filter 使用 `Piping\PipingRuns` | Phase 2 可用来设计选择集与范围读取；WorkingSet 和全模型范围仍须分别验证 |
| 最高操作温度 | 同一 Hexagon 教程读取 `GetPropertyValue("IJProcessDataInfo", "OperatingMaxTemp")`；`AutomationLabs 2016/frmTempAndPressureMod.vb` 用 `UnitType.Temperature` 和 UOM 管理器解析温度 | 最高操作温度的接口/属性名有较强示例依据；项目负责人已更正确认目标模型原始温度为 K。适配层换算为 °C，接口属性名仍须现场核对 |
| 设计温度 | `AutomationLabs_CSharp/PlaceEqpAndPipingForm.cs:142` 设置 `IJProcessDataInfo.DesignMaxTemp` | 报告中的设计温度可优先调查这个字段；设计温度仍不参与判定 |
| Spec 读取 | `HQHBNameRule/HQHBNameRule/CPipeRun.vb:4` 从 PipeRun 的 `PathRunUsesSpec`/`Spec` 关系取得对象，再读取 `IJDPipeSpec.SpecName`；`SP3D_API` wiki 的 `02_piping.md:199` 展示 `PipeRun.Specification` | 提供两条候选读取路径；在目标 Plant 中对同一 PipeRun 核对它们与界面显示的 Spec 一致，再选定实现。绝不根据部件材质推断 Spec 类别 |
| 保温 | 同一 `CPipeRun.vb` 读取 `IJRteInsulation.Purpose`；`SP3D_API/Query/Query/Query/Form1.vb:540` 读取 `IJRteInsulation.IsInsulated` 和 `run.InsulationThickness`，并对厚度调用 UOM 转换 | Purpose、IsInsulated、Thickness 是可调查的实际字段；H3/PP CodeList 原始值及 InsulationSpec 仍未确认 |
| Paint Code | RAG 只返回兄弟项目 `S3D_PaintingImporter/PaintingImporter.cs:243-245` 的 `IJCoatingInfo.CoatingRequirement` 与 `CoatingType` 写入 | 没有找到 C093U/TSA/NOPT 对应的最终目标属性或 CodeList ID；保持 `OWNER-TBD` |
| 埋地与特殊条件 | 针对 Underground、H3、PP、STD-ENG-MS012 的查询没有得到可靠的 PipeRun 属性映射或完整决策树 | 继续 `OWNER-TBD`；未知值保持 `REVIEW`，不得填入猜测常量 |

Phase 2 最小核验路径：在宿主内选一条已知 PipeRun，记录其 OID；逐项查看 `SupportsInterface`、关系目标、属性原值/显示值、UOM 单位和 CodeList ID，并与 S3D 属性窗口比对。只有核对一致的字段才进入 `S3DAdapter` 常量。
