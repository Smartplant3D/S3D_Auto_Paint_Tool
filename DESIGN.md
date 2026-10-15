# S3D Auto Paint Tool 设计

设计依据：仅 [SPEC.md](SPEC.md)。工程标准未提供的条件保持 `REVIEW`，不从名称或温度推断涂漆体系。

## 用户流程

1. 当前 S3D 命令扫描活动 Working Set 的全部 PipeRun，不保证覆盖整个项目；CSV 演示仍可独立运行。
2. 读取并校验四份配置。配置错误时整批停止，不触碰模型。
3. 读取 PipeRun 快照，记录 OID、来源属性、操作温度等原值。目标模型原始温度为 K；S3D 适配层按 `K - 273.15` 换算为 °C，不预先四舍五入，规则层只接受 °C。
4. 对每条 PipeRun 运行纯规则判定，预览候选代码、状态和原因。`REVIEW`、`MISMATCH`、`NOT_APPLICABLE` 不进入自动写入集合。
5. 导出 XLSX，供工程师补全映射或裁决多候选。`UnmappedSpecs` 汇总未映射等级及频次。
6. 写入前生成预览报告并显示拟修改数量，要求明确确认。宿主命令核对 Paint Code 的 CodeList；批量写入使用单事务，异常时 Abort，并把失败条目标为 `ERROR` 后导出结果报告。

## 组件边界

| 组件 | 职责 |
|---|---|
| `PaintRules` | 配置校验、Spec 精确匹配、保温分类、规则判定；无 S3D 依赖 |
| `Reporting` | 后台生成 XLSX，包含逐条结果、状态汇总、未映射 Spec |
| `App` | Phase 1 CSV 演示和运行元数据日志 |
| `S3DAdapter` | 读写契约和温度换算 |
| `S3DHost` | S3D 自定义命令、PipeRun 扫描、预览、CodeList 验证与事务写回 |

规则引擎遵循 SPEC §5 顺序：埋地 → Spec → 保温 → 特殊条件 → 温度与规则 → 候选唯一性 → 现值比较 → 写入意图。`UseLowestApplicableBand` 默认关闭。公司标准的特殊条件只触发复核；CS 有保温且操作温度高于 177°C 时只附加 N1 提示。

## 预览界面建议（Phase 2/3）

顶部显示项目、映射版本、规则版本、评估范围与 DryRun 状态。主体表格按 `Status`、Spec、管线和 PipeRun 筛选，详情区域显示温度来源、保温输入、全部候选和完整 Reason。底部显示各状态计数及“导出报告”。只有实际写入模式下，且存在可写的唯一结果时，显示“确认写入”；确认框必须列出将修改数量。所有 `REVIEW` 行应能导出，但不能在界面中手动绕过规则直接写入。

## 待目标 Plant 验证的信息

- 当前宿主命令已按兄弟项目的 `Piping\PipingRuns` 和 `IJCoatingInfo.CoatingType` 实现；需在目标 S3D 版本/Plant 验证。
- Spec、操作温度、保温的实际属性值和 CodeList；埋地标识的接口名尚未取得，默认全部标记 `REVIEW`。原始温度单位已更正确认为 K。
- H3/PP 的真实 CodeList 值；如模型没有埋地属性，是否启用 `IgnoreUnknownUnderground`。
- 完整 Spec 映射、最低档开关和多候选选择依据。
- 标准防腐逻辑图的连线及关键服务条件表。

当前全部项目目标框架为 `net462`。应用实际写回时，`AUTO_SET` 只能在事务成功提交后作为最终状态；规则引擎返回该值只表示符合自动写入条件。S3D 相关证据见 [SIBLING_EVIDENCE.md](SIBLING_EVIDENCE.md)。

本机 Smart3D RAG 的候选接口与待验证边界记录在 [RAG_EVIDENCE.md](RAG_EVIDENCE.md)。

用户提供的导入模板为 `IJCoatingInfo.CoatingType` 及全部 10 个规则 Paint Code ID 提供了直接线索，见 [TEMPLATE_EVIDENCE.md](TEMPLATE_EVIDENCE.md)。项目负责人确认原规则 `C093U-3` 等价于模板的 `C093U3`，规则配置已统一使用后者；目标 Plant CodeList 仍需在写入前核对。

全模型评估可能产生大量结果。接入 Phase 2 前须确认宿主进程位数和目标数据量；报告层应参考兄弟项目的流式 XLSX 写入与超出 Excel 单 Sheet 行数时的拆分策略。当前 Phase 1 报告导出器会在内存中保留所有结果，尚未针对全模型规模验证。
