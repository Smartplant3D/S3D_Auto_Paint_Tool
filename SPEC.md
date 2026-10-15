# SPEC.md — S3D Auto Paint Code Tool（自包含版，供 Codex 使用）

> 本文件已经包含实现所需的全部业务规则。Codex 无法访问公司规范，**只依据本文件实现**。
> 规则：本文件没有写到的工程判断，一律不实现推断逻辑，结果标记 `REVIEW`。
> 标记 **[OWNER-TBD]** 的内容由开发者（工程师）在配置文件或代码 TODO 中补充，Codex 不得编造。

---

## 0. 一句话需求

遍历 Smart 3D (S3D) 模型中的 **PipeRun**，根据
`PipeRun.Spec`（管道等级）→ 材质类别、操作温度、保温条件，
按本项目涂漆规则得到 Paint Code。结果唯一的写回 PipeRun，不唯一或缺数据的标记 REVIEW，最后输出 Excel 报告。

---

## 1. 领域知识（Codex 需要理解的背景）

| 术语 | 含义 |
|---|---|
| S3D / Smart 3D | Hexagon 的三维工厂设计软件，提供 .NET API |
| PipeRun | S3D 中一段管道的逻辑对象，携带 Spec、温度、保温、涂漆等属性 |
| Spec / Piping Materials Class | 管道等级代号，例如 `1CBB01`。一个 Spec 对应一种材料体系 |
| Paint Code | 涂装体系代号，例如 `C093U3`。`NOPT` = No Paint，不涂漆 |
| Exposed | 不保温（裸露） |
| Insulated | 有保温/保冷 |
| H3 | 一种特定保温类型。H3 保温的管道用单独的 Paint Code 列 |
| Personnel Protection (PP) | 防烫伤保温，只为防止人员烫伤，不是工艺保温 |
| Operating Temperature | 操作温度（选择涂漆用这个） |
| Design Temperature | 设计温度（仅报告参考，不用于选择） |
| CS | Carbon Steel 碳钢 |
| LAS | Low Alloy Steel 低合金钢 |
| SS | Stainless Steel 不锈钢 |
| AL | Aluminum 铝 |
| C3/C4/C5/CX | ISO 12944 大气腐蚀等级，C5/CX 为严重腐蚀环境 |
| CUI | Corrosion Under Insulation 保温层下腐蚀 |
| Underground | 埋地管道 |

---

## 2. 项目规则（项目 EN250186，规则来源已全部转写如下）

### 2.1 适用范围

1. 本规则**不适用于埋地管道（Underground piping）**。埋地 PipeRun → 状态 `NOT_APPLICABLE`，不修改。
2. 需要涂漆的材料：**Carbon Steel（保温的和不保温的）**。
3. 通常不需要涂漆（Paint Code = `NOPT`）的材料：
   - Stainless Steel（保温和不保温）
   - Aluminum
   - Galvanized Carbon Steel（镀锌碳钢）
   - HDPE
   - CPVC
   - Monel
   - Copper and Copper Alloys
4. 项目环境要求（仅写入报告/配置，第一版不参与计算）：
   - 室外：L5 in C5 及 L20 in C4
   - 室内设备：L20 in C3

### 2.2 项目 Paint Code 表（Table 1，原样转写）

| 行 | 温度 Min °C | 温度 Max °C | (°F Min) | (°F Max) | CS Exposed | CS Insulated | CS H3 | SS/AL Exposed | SS/AL Insulated | SS/AL H3 |
|---|---|---|---|---|---|---|---|---|---|---|
| R1 | -40 | 93 | -40 | 200 | C093U3 | C093N | C093U3 | NOPT | NOPT | NOPT |
| R2 | -40 | 230 | -40 | 460 | C230U **或** TSA230U | C230N **或** TSA230N | C230U | NOPT | NOPT | NOPT |
| R3 | -40 | 400 | -40 | 752 | C400U **或** TSA400U | TSA400N | C400U | NOPT | NOPT | NOPT |
| R4 | -196 | 649 | -320 | 1200 | –（无） | –（无） | –（无） | NOPT | NOPT | NOPT |

表注：
- H3 保温的管道和设备使用 **H3 列**。
- 程序**一律使用 °C 列**。°F 列仅作参考（原表 230°C 对应写的是 460°F，按换算应为 446°F，不影响计算，在报告/日志中忽略）。
- “–” 表示该温度段没有 CS 的 Paint Code。
- R1 的项目规则曾写为 `C093U-3`；项目负责人确认它与 S3D 模板中的 `C093U3` 等价。程序和配置统一使用 CodeList 名称 `C093U3`（无尾随空格）。

### 2.3 公司标准中的防腐判断逻辑（STD-ENG-MS012 Appendix A Figure 1, Rev.2，转写节点）

该逻辑图用于“碳钢、低合金钢、不锈钢在保温层下的防腐（CUI）”，依据**操作温度、材料、关键服务条件**。图中出现的判断节点和结果如下：

| 节点 | 内容 |
|---|---|
| N1 | 是否“连续操作温度 > 177°C (350°F)”，**或**“完全位于干燥建筑内，且工艺温度始终比环境露点高 5°C (9°F) 以上” → Yes 为 **No Paint** |
| N2 | 按表面材料分支：Carbon and Low Alloy Steel / Stainless Steel |
| N3 | 是否“连续操作温度 < -4°C (25°F)” |
| N4 | 是否“连续操作温度 < 50°C (122°F)” |
| N5 | Key Service Condition（关键服务条件，引用标准 Table 1，原表内容未提供） |
| N6 | 是否“C5、CX 或现场特定微环境” |
| N7（SS） | 是否“暴露于空气盐雾或其他形式的氯化物” |
| 结果 | Paint / No Paint |

**实现要求：**
- 节点之间连线的具体走向（每个 Yes/No 去哪）没有可靠转写，**Codex 不得自行连接这些节点生成决策树**。
- 第一版只把这些条件作为**可选的输入标记（flags）**：如果 PipeRun 上有任何一个特殊条件标记为 true，或标记值未知且材料为 CS/LAS/SS 且有保温，则结果 = `REVIEW`，原因写明对应节点。
- 预留 `IStdCorrosionLogic` 接口，默认实现 `ReviewOnSpecialConditions`，以后由工程师补完整决策树。
- 唯一直接可用的确定规则：N1 中的“连续 > 177°C 且有保温”在逻辑图中通向 No Paint，**但它与项目 Table 1（CS Insulated 230/400 档有 Paint Code）冲突**。项目规则优先，第一版**不使用 N1 自动判 NOPT**；当 CS + Insulated + T > 177°C 时，在报告 Reason 中加提示 `"STD-ENG-MS012 N1: >177°C continuous may allow No Paint - engineer to confirm"`，状态仍按项目表计算。

### 2.4 温度取值规则

- 用 **操作温度**（优先最高操作温度 Max Operating Temperature；没有则用 Operating Temperature）。
- 不使用设计温度判断；设计温度只输出到报告。
- 温度缺失 → `REVIEW`。
- 区间判断为闭区间：`TempMin <= T <= TempMax`。

### 2.5 温度档重叠的处理

R1/R2/R3 的区间都从 -40 开始，是**重叠**的（60°C 同时落在 R1、R2、R3）。
- 配置项 `UseLowestApplicableBand`：
  - `true`：取包含 T 的最低档（BandRank 最小），例如 60°C 只取 R1；150°C 取 R2；300°C 取 R3。
  - `false`（默认）：把所有匹配档的 Paint Code 都当作候选，候选 > 1 个 → REVIEW。
- 默认值 `false`，[OWNER-TBD] 由工程师确认后改为 `true`。

### 2.6 同档多个 Paint Code

R2 Exposed（C230U/TSA230U）、R2 Insulated（C230N/TSA230N）、R3 Exposed（C400U/TSA400U）各有两个可选代码，选择依据没有提供。
→ 输出全部候选，`REVIEW`，**不自动写入**。（`PaintingRules.csv` 中 `AutoAssignable = N`）

### 2.7 保温条件分类

| S3D 保温信息 | InsulationCondition |
|---|---|
| 没有保温（Purpose 为空/None，或厚度 = 0） | Exposed |
| H3 保温 [OWNER-TBD：H3 在 S3D 中的识别方式，例如 Insulation Spec/Material/Purpose 的某个值] | H3 |
| 其他工艺保温/保冷（Heat Conservation、Cold Conservation 等） | Insulated |
| Personnel Protection 防烫保温 | 由配置 `PersonnelProtectionAs` 决定：`Exposed` / `Insulated` / `Review`（默认 `Review`）|
| 无法识别的值 | Review |

保温 Purpose 值到分类的映射放在 `InsulationMapping.csv`，不在代码里写死。

---

## 3. 材质判断规则

### 3.1 规则

1. 材质判断的**唯一输入是 `PipeRun.Spec`**（例如 `1CBB01`）。
2. 不读取管子、管件、法兰的 ASTM 材料。
3. Spec 规范化：`Trim()` + `ToUpperInvariant()`。
4. 查 `SpecMaterialMapping.csv`，**精确匹配**。
5. **禁止**按字符串规则猜测（不得用 Contains / StartsWith / 正则 / 首字母推断材质）。
6. 未找到 → `MaterialCategory = Unknown`，状态 `REVIEW`，不修改模型。
7. Status = `Obsolete` → `REVIEW`，原因 “Obsolete piping class”。

### 3.2 材质类别枚举

`CS, LAS, SS, AL, NI, CU, GALV, HDPE, CPVC, OTHER, UNKNOWN`

（NI = 镍合金如 Monel；CU = 铜及铜合金；GALV = 镀锌碳钢）

### 3.3 已知映射（初始数据）

| Spec | Category | MaterialGroup | Status | 依据 |
|---|---|---|---|---|
| 1CBB01 | CS | Carbon Steel | Active | 项目 BOM 部件为 A234-WPB、A105N、A671-CB60 |
| 1ALA01 | AL | Aluminum Alloy 5083, O temper | Active | 管道等级 Material Group = AL |
| WSC020 | SS | Stainless Steel 304/304L | Obsolete | 旧等级 Rev.10 已作废 |
| ZCSC81 | CS | Carbon Steel | Project | 埋地循环水 |

其余 Spec：[OWNER-TBD] 由工程师补全。为此工具需要提供一个**导出功能**：导出模型中所有不重复的 PipeRun.Spec 及其在映射表中的状态（已映射/未映射），方便工程师填表。

---

## 4. 配置文件（全部放在 `config/` 目录，UTF-8，带表头）

### 4.1 `SpecMaterialMapping.csv`

```csv
Spec,MaterialCategory,MaterialGroup,Status,Source,Revision,Notes
1CBB01,CS,Carbon Steel,Active,Project BOM,,
1ALA01,AL,Aluminum Alloy 5083 O temper,Active,PCS Workbook,,
WSC020,SS,Stainless Steel 304/304L,Obsolete,PCS,Rev.10,Do not use
ZCSC81,CS,Carbon Steel,Project,PCS,,Underground CW
```
校验：Spec 不得重复；MaterialCategory、Status 必须是合法枚举；Status ∈ `Active, Project, Obsolete`。

### 4.2 `PaintingRules.csv`

```csv
Project,MaterialCategory,InsulationCondition,TempMin,TempMax,BandRank,PaintCode,AutoAssignable,Notes
EN250186,CS,Exposed,-40,93,1,C093U3,Y,
EN250186,CS,Insulated,-40,93,1,C093N,Y,
EN250186,CS,H3,-40,93,1,C093U3,Y,
EN250186,CS,Exposed,-40,230,2,C230U,N,Alternative TSA230U
EN250186,CS,Exposed,-40,230,2,TSA230U,N,Alternative C230U
EN250186,CS,Insulated,-40,230,2,C230N,N,Alternative TSA230N
EN250186,CS,Insulated,-40,230,2,TSA230N,N,Alternative C230N
EN250186,CS,H3,-40,230,2,C230U,Y,
EN250186,CS,Exposed,-40,400,3,C400U,N,Alternative TSA400U
EN250186,CS,Exposed,-40,400,3,TSA400U,N,Alternative C400U
EN250186,CS,Insulated,-40,400,3,TSA400N,Y,
EN250186,CS,H3,-40,400,3,C400U,Y,
EN250186,SS,*,-196,649,4,NOPT,Y,
EN250186,AL,*,-196,649,4,NOPT,Y,
EN250186,GALV,*,,,0,NOPT,Y,Typically no paint
EN250186,HDPE,*,,,0,NOPT,Y,Typically no paint
EN250186,CPVC,*,,,0,NOPT,Y,Typically no paint
EN250186,NI,*,,,0,NOPT,Y,Monel - typically no paint
EN250186,CU,*,,,0,NOPT,Y,Typically no paint
```
说明：
- `*` = 任意保温条件。
- TempMin/TempMax 为空 = 不限温度。
- LAS、OTHER 没有规则行 → 无匹配 → REVIEW。
- CS 温度 < -40 或 > 400 → 无匹配 → REVIEW。
- SS/AL 温度超出 -196~649 → REVIEW。

### 4.3 `InsulationMapping.csv`

```csv
S3DInsulationPurpose,InsulationCondition
,Exposed
None,Exposed
Heat Conservation,Insulated
Cold Conservation,Insulated
Personnel Protection,PP
H3,H3
```
[OWNER-TBD] 用工程师从 S3D CodeList 导出的实际值替换（左列为 S3D 中的显示值或代码值）。`PP` 再由 `PersonnelProtectionAs` 决定。

### 4.4 `appsettings.json`

```json
{
  "Project": "EN250186",
  "MappingVersion": "OWNER-TBD",
  "PaintRuleVersion": "EN250186-MSE-FL1-00008 Rev.0 (23-JUL-2025)",
  "UseLowestApplicableBand": false,
  "PersonnelProtectionAs": "Review",
  "DryRun": true,
  "OverwriteExistingPaintCode": false
}
```
- `OverwriteExistingPaintCode = false`：PipeRun 已有 Paint Code 且与计算结果不同 → 不覆盖，状态 `MISMATCH`。

---

## 5. 判定算法（必须严格按此实现）

```text
Evaluate(run):
  if run.IsUnderground == true          → NOT_APPLICABLE
  if run.IsUnderground == null          → REVIEW "Underground flag unknown"   [若模型无此属性可配置为忽略]

  spec = Normalize(run.Spec)
  if spec empty                         → REVIEW "Spec missing"
  m = SpecMap.TryGet(spec)
  if m == null                          → REVIEW "Spec not mapped"
  if m.Status == Obsolete               → REVIEW "Obsolete piping class"

  ins = InsulationMap(run.InsulationPurpose, run.InsulationThickness)
  if ins == PP: ins = settings.PersonnelProtectionAs
  if ins == Review                      → REVIEW "Insulation condition unresolved"

  if any special-condition flag true (N1..N7)  → REVIEW "STD-ENG-MS012 special condition: ..."

  T = run.MaxOperatingTemp ?? run.OperatingTemp
  rows = Rules.Where(Project == settings.Project
                     && MaterialCategory == m.Category
                     && (InsulationCondition == ins || InsulationCondition == "*")
                     && (TempMin empty || T >= TempMin)
                     && (TempMax empty || T <= TempMax))
  if rows need T and T == null          → REVIEW "Operating temperature missing"
  if rows empty                         → REVIEW "No rule for material/insulation/temperature"

  if settings.UseLowestApplicableBand:
      rows = rows.Where(BandRank == rows.Min(BandRank))

  codes = rows.Select(PaintCode).Distinct()
  if codes.Count > 1 or rows.Any(AutoAssignable == N)
                                        → REVIEW "Multiple candidates: ..." (列出 codes)

  code = codes.Single()
  existing = run.PaintCode
  if existing == code                   → OK
  if existing not empty and !OverwriteExistingPaintCode → MISMATCH
  if settings.DryRun                    → WOULD_SET
  else write code                       → AUTO_SET（写入失败 → ERROR）
```

状态枚举：`OK, AUTO_SET, WOULD_SET, MISMATCH, REVIEW, NOT_APPLICABLE, ERROR`

---

## 6. 软件结构

```
S3DPaintTool.sln
├─ PaintRules (class library, 无 S3D 依赖)
│   ├─ Models/ PipeRunData, MaterialCategory, InsulationCondition, EvaluationResult, Status
│   ├─ Config/ ConfigLoader（加载+校验 4 个配置文件）
│   ├─ SpecMaterialMapper
│   ├─ InsulationClassifier
│   ├─ IStdCorrosionLogic + ReviewOnSpecialConditions
│   └─ PaintRuleEngine（纯函数 Evaluate）
├─ PaintRules.Tests (单元测试)
├─ S3DAdapter (引用 S3D DLL)
│   ├─ IS3DReader  → S3DReader   读取 PipeRun → PipeRunData
│   └─ IS3DWriter  → S3DWriter   写 Paint Code（事务）
├─ Reporting/ ExcelReportExporter
└─ App (S3D 命令或 WinForms 界面：选择范围 → 评估 → 预览 → 确认写入 → 导出报告)
```

`PipeRunData` 字段：
```
Oid, Name, PipelineName, Spec, MaxOperatingTemp?, OperatingTemp?, DesignTemp?,
InsulationPurpose, InsulationThickness?, InsulationSpec, IsUnderground?,
SpecialFlags (Dictionary<string,bool?>), ExistingPaintCode
```

---

## 7. S3D 接口约束

- 通过 **S3D .NET API** 读写（BusinessObject 属性 + ClientServiceProvider 事务 Commit/Abort）。**禁止直接写 SQL UPDATE 数据库。**
- 下列 S3D 属性的接口名/属性名/CodeList 值 [OWNER-TBD]，在 `S3DAdapter` 中集中定义为常量并加 TODO：
  - Spec（Piping Materials Class）
  - Max Operating Temperature / Operating Temperature / Design Temperature 的属性名 [OWNER-TBD]；目标模型的原始温度单位为 K（项目负责人 2026-10-15 更正确认），读取后按 `K - 273.15` 换算为 °C，不预先四舍五入
  - Insulation Purpose / Thickness / Spec（H3 识别）
  - Underground 标识
  - Paint Code 写入的目标属性：用户提供的 `PaintingImport_Template.xlsx` 指向 `IJCoatingInfo.CoatingType`（示例 C 列 `10011`，J208 为 `10011 = NOPT`）；仍需在目标 Plant 的 PipeRun 上验证接口和 CodeList。规则已统一为模板名称 `C093U3`，10 个规则代码均与模板精确匹配，详见 `TEMPLATE_EVIDENCE.md`
- 如果 Paint Code 是 CodeList 属性，写入前检查值存在；不存在 → ERROR “Paint code not in CodeList”。
- 写入在单个事务中批量执行；任何异常 → Abort 并报告。
- 读取若需要 SQL：只读查询，不建临时表，使用 CTE；SQL Server 2022。

## 8. 其他技术要求

- C#，Visual Studio 2022；.NET Framework 4.6.2（项目负责人 2026-10-15 确认；S3D 版本仍待确认）。
- 代码中不得出现 Spec 名称或 Paint Code 字面量（测试和示例配置除外）。
- 默认 DryRun；写入前弹出确认（显示将要修改的数量）。
- 报告导出在后台线程；数值保持原精度；打开输出文件夹前校验路径存在且在允许目录内。
- 日志记录每次运行的 MappingVersion、PaintRuleVersion、用户、时间、范围。
- 运行环境含 Citrix：不依赖本地安装路径，配置目录相对程序目录。

## 9. Excel 报告列

`PipeRun, Pipeline, Spec, MaterialCategory, MaterialGroup, SpecStatus, MaxOperatingTemp_C, OperatingTemp_C, DesignTemp_C, InsulationPurpose, InsulationCondition, ExistingPaintCode, CandidatePaintCodes, ResultPaintCode, Status, Reason, MappingVersion, PaintRuleVersion, RunTime, User`

附加 Sheet：
- `Summary`：各 Status 计数。
- `UnmappedSpecs`：未映射的 Spec 及出现次数。

## 10. 单元测试（PaintRuleEngine，使用 4.1~4.4 示例配置）

| # | Spec | T °C | Insulation | LowestBand | 期望 |
|---|---|---|---|---|---|
| 1 | 1CBB01 | 60 | Exposed | true | WOULD_SET C093U3 |
| 2 | 1CBB01 | 60 | Exposed | false | REVIEW（候选 C093U3, C230U, TSA230U, C400U, TSA400U）|
| 3 | 1CBB01 | 60 | Insulated | true | WOULD_SET C093N |
| 4 | 1CBB01 | 60 | H3 | true | WOULD_SET C093U3 |
| 5 | 1CBB01 | 93 | Exposed | true | WOULD_SET C093U3（边界含 93）|
| 6 | 1CBB01 | 150 | Exposed | true | REVIEW（C230U, TSA230U）|
| 7 | 1CBB01 | 150 | Insulated | true | REVIEW（C230N, TSA230N）|
| 8 | 1CBB01 | 150 | H3 | true | WOULD_SET C230U |
| 9 | 1CBB01 | 300 | Insulated | true | WOULD_SET TSA400N，Reason 含 N1 提示 |
| 10 | 1CBB01 | 300 | Exposed | true | REVIEW（C400U, TSA400U）|
| 11 | 1CBB01 | 300 | H3 | true | WOULD_SET C400U |
| 12 | 1CBB01 | 450 | Exposed | true | REVIEW（无规则）|
| 13 | 1CBB01 | -50 | Insulated | true | REVIEW（无规则）|
| 14 | 1CBB01 | null | Exposed | true | REVIEW（缺温度）|
| 15 | 1ALA01 | 20 | Insulated | any | WOULD_SET NOPT |
| 16 | 1ALA01 | null | Exposed | any | REVIEW（缺温度，因 AL 规则有温度范围）|
| 17 | WSC020 | 20 | Exposed | any | REVIEW（Obsolete）|
| 18 | 1XYZ99 | 20 | Exposed | any | REVIEW（未映射）|
| 19 | " 1cbb01 " | 60 | Insulated | true | 同 #3 |
| 20 | 1CBB01 | 60 | PP（PersonnelProtectionAs=Review）| true | REVIEW |
| 21 | 1CBB01 | 60 | PP（PersonnelProtectionAs=Exposed）| true | WOULD_SET C093U3 |
| 22 | 1CBB01 | 60 | Exposed，IsUnderground=true | any | NOT_APPLICABLE |
| 23 | 1CBB01 | 60 | Insulated，特殊标记 C5=true | true | REVIEW |
| 24 | 1CBB01 | 60 | Insulated，现值 C093N | true | OK |
| 25 | 1CBB01 | 60 | Insulated，现值 C230N，不覆盖 | true | MISMATCH |
| 26 | 1CBB01 | 60 | Insulated，DryRun=false | true | AUTO_SET C093N |
| 27 | 配置中 Spec 重复 | – | – | – | ConfigLoader 抛出校验异常 |

## 11. 实施阶段

**Phase 1（立即做，不需要 S3D）**：PaintRules 库、ConfigLoader、单元测试（第 10 节全部通过）、Excel 报告、命令行 Demo（从 CSV 读入模拟 PipeRun 数据 → 输出报告）。
**Phase 2**：S3DReader（只读）+ 导出模型中所有 Spec 清单 + 只读评估报告。
**Phase 3**：S3DWriter（事务写入、DryRun、确认对话框）。

## 12. [OWNER-TBD] 汇总

1. 全部 Spec → 材质映射数据。
2. S3D 属性名、目标 Plant 的 CodeList 值（第 7 节）；`IJCoatingInfo.CoatingType` 与 10 个代码 ID 有模板依据，但尚未在目标 Plant 验证。目标模型原始温度单位已更正确认为 K。
3. H3 和 Personnel Protection 在 S3D 中的识别值。
4. 是否启用 `UseLowestApplicableBand`。
5. C230U/TSA230U、C230N/TSA230N、C400U/TSA400U 的选择规则。
6. STD-ENG-MS012 逻辑图的完整连线及 Key Service Condition Table 1 内容。
7. S3D 版本待确认；.NET Framework 已确认为 4.6.2。

---

## 给 Codex 的第一条指令

```text
Read SPEC.md fully. It is the only source of business rules; do not invent engineering logic.
Implement Phase 1 (section 11) in C#:
- PaintRules class library per section 6, algorithm exactly per section 5.
- Config files per section 4 placed in /config with the sample data shown.
- Unit tests covering every case in section 10.
- ExcelReportExporter per section 9.
- A console demo reading sample PipeRun rows from /samples/piperuns.csv.
Define IS3DReader/IS3DWriter interfaces only, with TODO comments for section 12 items.
Never infer material from spec text; never hard-code spec names or paint codes outside tests/config.
```
