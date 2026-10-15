# PaintingImport_Template.xlsx 核对结果（2026-10-15）

来源：用户提供的 `C:\Users\Payne\Downloads\PaintingImport_Template.xlsx`，工作表 `PaintingImport`。本项目仅读取该文件，未修改。模板含 185 条导入示例（第 3–187 行）及右侧 CodeList 说明（I–K 列）。

第 1 行的 B/C 列分别为 `CoatingRequirement`、`CoatingType`。第 2 行注明二者均属于 `IJCoatingInfo` CodeList。185 条示例的 B 列均为 `10001`，右侧 I8 给出 `10001 = Air Products`；C 列均为 `10011`，右侧 J208 给出 `10011 = NOPT`。因此，这份模板直接支持“Paint Code 候选值在 `CoatingType` 中”的判断。实际 PipeRun 是否支持此接口、目标 Plant 中 ID 是否一致，仍须在 S3D 宿主内验证。

## 与项目规则表的精确比较

比较本项目 `config/PaintingRules.csv` 中全部 10 个不同的 Paint Code 与模板 J 列说明，按**完全相同的名称**匹配：

| 规则 Paint Code | 模板 CoatingType ID | 模板位置 | 结论 |
|---|---:|---|---|
| NOPT | 10011 | J208 | 精确匹配 |
| C093U3 | 10063 | J258 | 精确匹配；原规则写法 `C093U-3` 已由项目负责人确认为等价 |
| C093N | 10043 | J238 | 精确匹配 |
| C230U | 10044 | J239 | 精确匹配 |
| C230N | 10045 | J240 | 精确匹配 |
| C400U | 10048 | J243 | 精确匹配 |
| TSA230U | 10059 | J254 | 精确匹配 |
| TSA230N | 10060 | J255 | 精确匹配 |
| TSA400U | 10061 | J256 | 精确匹配 |
| TSA400N | 10062 | J257 | 精确匹配 |

模板 J258 是 `10063 = C093U3`。项目负责人于 2026-10-15 确认原规则写法 `C093U-3` 与其等价，并要求规则表改为 `C093U3`。当前规则已统一采用无尾随空格的 CodeList 名称；不在程序中保留按连字符猜测的模糊匹配。

模板列出的 ID 是这份文件中的静态说明，不是对当前 Plant CodeList 的实时查询。正式写回必须重新检查目标属性支持、代码名称和 ID 是否存在，并读回确认。`CoatingRequirement=10001` 是示例输入；本项目 SPEC 仅要求写 Paint Code，不授权覆盖该字段。模板 A 列是 `PipeRunName`，写回对象应以模型 OID/已验证对象引用定位，不能依赖名称模糊查找。
