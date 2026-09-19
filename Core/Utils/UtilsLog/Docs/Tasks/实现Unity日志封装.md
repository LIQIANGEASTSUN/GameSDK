# 实现 Unity 日志封装

状态：`Done`；实现与验证结果见[执行记录与结果](#执行记录与结果)。

## 产物与前置

按[需求](../需求.md)交付 `Core/Utils/UtilsLog/UtilsLog.cs`、`Core/Utils/UtilsLog/Tests/` 测试及元数据，均相对包根；`UtilsLog.cs` 同时承载 `UtilsLog` 和 `LogLevel` 两个公开类型。测试使用独立程序集；运行时复用 `GameSDK.asmdef`，由[普通类单例任务](../../../../Singleton/Docs/Tasks/实现普通类单例.md)提供或移交写入权，不依赖单例类型。

## API 与接入

本任务与[需求](../需求.md)共同规定实现与验收，不以现有源码或测试替代规范。实现直接使用 Unity 日志，不引入单例、接口或第三方依赖。

命名空间为 `GameSDK`，公开类型为 `public static class UtilsLog` 和 `public enum LogLevel`。枚举成员为 `Info = 0`、`Warning = 1`、`Error = 2`。全部入口如下，无需实例、组件或显式初始化：

| 公开签名 | 接入含义 |
|---|---|
| `public static void SetEnabled(bool enabled)` | 修改模块总开关。 |
| `public static void SetLevel(LogLevel level)` | 修改模块最低输出等级。 |
| `public static void Log(string message)` | 对应 `Debug.Log`，按 `Info` 等级过滤。 |
| `public static void Warning(string message)` | 对应 `Debug.LogWarning`。 |
| `public static void Error(string message)` | 对应 `Debug.LogError`。 |
| `public static void Exception(System.Exception exception, UnityEngine.Object context = null)` | 对应 `Debug.LogException`，按 `Error` 等级过滤。 |

默认值、过滤矩阵、重复修改、参数错误及输出失败以[需求的行为与验收](../需求.md#行为与验收)为准。配置字段为 `private static bool _enable = true` 和 `private static LogLevel _level = LogLevel.Info`，直接声明初值；关闭直接过滤所有合法输出，开启时只放行不低于阈值的等级。`SetLevel` 先校验 `level`，非法值抛 `ArgumentOutOfRangeException`；`Exception` 先拒绝空异常并抛 `ArgumentNullException`，再判断过滤，不能由关闭状态绕过校验。参数失败不改变配置。

每次输出读取当前配置并在 Unity 转发前过滤；普通日志将原字符串直接交给对应 `Debug` 方法，不额外格式化，异常日志将原异常与可选上下文直接交给 `Debug.LogException`，不包裹底层异常。包装层放行不承诺 Console 可见性。重复配置、不缓存／补发、Unity 全局设置与异常传播边界均按需求执行。

### 调用与会话

允许引用 GameSDK 的业务与包内模块直接调用上述 API，例如：

```csharp
using GameSDK;

UtilsLog.Log("开始加载资源");
UtilsLog.SetLevel(LogLevel.Warning);
UtilsLog.Warning("资源尚未就绪");
UtilsLog.Error("资源加载失败");
UtilsLog.SetEnabled(false);
```

配置初值由静态字段初始化提供，不设置自动会话重置方法或初始化属性。新脚本域从开启／Info 开始；关闭 Domain Reload 时，连续 Play 保留当前开关与等级。需要每次 Play 固定配置的宿主可显式设置二者，例如：

```csharp
using GameSDK;
using UnityEngine;

public static class ProjectLogConfiguration
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
        UtilsLog.SetEnabled(true);
        UtilsLog.SetLevel(LogLevel.Warning);
    }
}
```

宿主也可在 `Awake`／`Start` 设置配置；仅修改等级不会重新开启已关闭的输出。脚本域与静态字段行为见 [Unity Domain Reload 说明](https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html)。

## 实施要点

1. `UtilsLog` 持有开关和阈值，先校验非法等级或空异常，再读写状态或输出；比较本模块等级，不比较 Unity `LogType` 数值。
2. 每次输出均按当前开关和阈值决定是否转发；普通日志只接收原字符串，不增加消息转换或格式化层。保留运行时切换，不加 `Conditional`、仅 Editor 条件或第三方依赖。
3. `_enable` 与 `_level` 直接声明初值，不另设默认值常量或会话重置方法；宿主按需显式配置。

## 必要验证与交接

在隔离环境中串行执行测试。初始默认值必须在真实初始化完成后、任何 `UtilsLog` 配置调用前，通过实际转发行为观测；其余规则用例设置明确的固定基线（开启、阈值 `Info`），并在 `finally` / teardown 恢复该基线。测试前仍须保存 Unity 全局配置和日志处理器，结束时恢复；普通字符串按处理器收到的内容观测，不通过新增查询或重置 API 验证。

- **静态检查**：API、程序集隔离与元数据完整；三个普通输出仅接收单个字符串，过滤先于转发，原字符串交给 Debug，运行时代码不改 Unity 全局设置，各构建类型保留调用路径。
- **EditMode 规则测试**：通过实际转发结果覆盖矩阵、默认值、反复修改与重新开启、非法输入；验证配置修改立即影响后续输出，重新开启保留阈值，非法等级不改变配置，空异常不受开关影响且始终抛错。用调用计数验证不补发、被过滤时不转发；不得用主动设置默认基线代替真实初始默认值验证。
- **输出与失败测试**：观测 Unity 类型、原字符串内容（含 `null`、空字符串）及原异常，异常入口另验证可选上下文；处理器抛错须原样传播且无递归报告。分别改变包装层、Unity 全局过滤和 Console 可见性，核对转发及其他直接 Unity 日志，不能只凭 Console 无记录判断过滤。
- **真实 Unity / Play 验证**：确认异常类型、消息、原始抛出位置及异常上下文对象定位；修改配置后再次 Play，通过实际转发结果分别验证启用 Domain Reload 时字段重新初始化、关闭时保留配置，并验证宿主随后显式配置生效。

Unity 导入、编译及测试由用户执行，Agent 提供步骤；必要项未完成时注明待用户验证，不标 `Done`。交付后保留上述规范正文，在下方分开记录实际结果与独立审查。[事件总线任务](../../../../EventBus/Docs/Tasks/实现事件总线.md)负责接入 `UtilsLog.Exception`、报告失败捕获、重入保护及联合验收；报告失败或重入时的输出边界由该任务规定，不另设备用日志出口。

## 执行记录与结果

以下历史记录中的状态与待验证事项反映各阶段情况；最新完成结论以“2026-09-19 用户验证确认”为准。

此前按用户要求移除旧实现并将任务恢复为 `NotStarted`。本轮开工时尚无源码、测试或可调用 API；包级 [GameSDK.asmdef](../../../../../GameSDK.asmdef) 已存在，可按上述约定复用。

### 2026-09-17 本轮实施

- 用户已授权开始实现本任务。开工确认 Log 目录仅有需求、Task 及元数据；工作区已有包目录迁移及其他模块改动，本轮保留这些改动，仅写入 Log 模块。
- 主 Agent 与实现 Agent `implement_log` 对齐现有规范；实现 Agent 唯一写入运行时代码、测试及新增元数据，主 Agent 负责本节执行记录和独立检查。复用已有 `GameSDK.asmdef`，不修改共享程序集。
- 必要检查包含静态 API／转发／元数据检查、EditMode 规则与失败测试，以及真实 Unity 的异常定位、Console 可见性和两种 Domain Reload 设置下的配置生命周期。Unity 导入、编译和运行测试按协作约定由用户执行，未取得结果前保持 `InProgress`。

#### 已交付与检查结果

- 运行时：[UtilsLog.cs](../../UtilsLog.cs) 承载 `UtilsLog` 与 `LogLevel` 两个公开类型。提供六个公开方法、共享开关与最低等级、空异常校验及 Unity 原样转发；静态初始化规则见下方“接口与初始化调整”，当前字段名和等级校验差异见 2026-09-19 记录。
- 测试：[独立 Editor 测试程序集](../../Tests/EditMode/GameSDK.Log.EditModeTests.asmdef)、[日志捕获夹具](../../Tests/EditMode/LogCapture.cs)、[36 个规则用例](../../Tests/EditMode/UtilsLogTests.cs)、[1 个首次默认状态用例](../../Tests/EditMode/UtilsLogInitialStateTests.cs)。用处理器调用与内容验证过滤矩阵、配置切换、参数失败、字符串、异常及上下文、处理器失败和 Unity 全局配置边界；首次默认用例须显式单独执行。
- 手动验证消费者：[UtilsLogPlayProbe.cs](../../Tests/EditMode/UtilsLogPlayProbe.cs)。通过 `Tools > GameSDK > Log` 菜单启用，默认不执行验证；Arm 显式设置关闭／Error，进入 Play 后按 Domain Reload 设置验证字段重新初始化或配置保留，再验证随后配置，并留下关闭／Error 状态供下一会话验证。异常菜单提供真实抛出位置和可定位的临时 GameObject，在退出 Play 及程序集重载前清理。
- 初轮主 Agent 已对照当时需求与 Task 独立检查运行时与测试、参数及输出失败路径、会话重置、程序集隔离、全局日志状态恢复和探针资源清理；后续发现及修正见下方记录。初轮静态 API、JSON 配置、元数据完整性／GUID 唯一性、文本格式及文档链接检查通过，不能代替 Unity 编译与执行结果，也不能代替用户最新修改后的复查。
- 新增源码与目录的元数据已齐；保留工作区自动出现的 `.meta` 及其 GUID，只补缺失项。本轮未启动 Unity、编译或运行测试，也未提交 Git 改动。

#### 必要验证步骤

使用项目指定的 Unity `6000.0.60f1`，串行验证，记录实际通过／失败／未执行。本项目使用嵌入包，无需修改 manifest 的 `testables`，见 [Unity 包测试说明](https://docs.unity3d.com/6000.0/Documentation/Manual/cus-tests.html)。

1. **导入、编译与首次默认状态**：确认无编译错误；在新加载脚本域中，任何 `UtilsLog` 配置调用、普通规则测试或手动探针之前，在 Test Runner → EditMode 单独选中 `GameSDK.Tests.UtilsLogInitialStateTests.FreshDomainForwardsEveryLevelBeforeAnyConfiguration` 并运行。预期该用例实际通过，四种输出均被转发；它带 `Explicit`，普通 Run All 中跳过不能算通过。若已运行其他验证，重新打开工程取得新脚本域后再验证本项，不能以测试设置的基线证明初始值。
2. **规则与失败路径**：选中 `GameSDK.Tests.UtilsLogTests` 运行全部 36 个用例，预期均通过；不要同时运行其他会修改全局 logger 的测试或启用 Play 探针。夹具在每例后恢复模块基线及先前的 Unity 日志设置、处理器。
3. **两种 Domain Reload 设置下的重复 Play**：使用无其他 `UtilsLog` 配置消费者的隔离空场景，记下原场景与 Enter Play Mode 设置，保持 Scene Reload 开启。先开启 Domain Reload，再执行 `Tools > GameSDK > Log > Arm Play Session Checks`，菜单会显式设置关闭／Error；连续进入／退出 Play 两次，预期每次通过字段重新初始化为开启／Info 的验证。随后关闭 Domain Reload，重新 Arm 设置关闭／Error 并清空计数，再连续进入／退出 Play 两次，预期每次先验证全部输出关闭，再仅开启输出并验证阈值仍为 Error。探针随后显式配置 Warning，验证即时生效，末尾留下关闭／Error。每组预期分别出现第 1、2 次 `UtilsLog Play PASS`，任何 `FAIL` 均需反馈。每组两次之间不运行规则测试、不 Disarm、不重新 Arm、不修改脚本或触发重编译，以保持连续会话验证条件。
4. **真实异常与 Console 可见性**：探针启用且处于 Play 时执行 `Emit Exception With Context`，Console 应保留 `InvalidOperationException`、`UtilsLog original exception / 原始异常定位验证` 消息及 `ThrowOriginalProbeException` 原始抛出位置；点击该条目的上下文应定位 `UtilsLog exception context` 对象。此条红色异常是预期验证输出。关闭 Console 对应等级按钮再发出一次，重新打开按钮后应能看到已产生的记录；同样切换普通 Log 可见性观察直接 Unity 输出的 Play PASS。Console 隐藏不等于包装层未转发，包装层与 Unity 全局过滤的调用计数由第 2 项验证。
5. **收尾**：退出 Play，确认临时上下文对象已清理，执行 `Disarm Play Session Checks` 恢复模块开启／Info，恢复原场景、Console 和 Enter Play Mode 设置。汇报首次默认用例、36 个规则用例、两组重复 Play、异常定位及 Console 观察的结果。

#### 复查发现与热重载清理修正

- 后续主 Agent 与独立评审 Agent `review_log` 确认 P3：异常菜单创建 `HideFlags.DontSave` 上下文对象后，若在 Play 内重编译并继续运行，静态引用会丢失；原先仅在退出 Play／Disarm 时清理，无法回收该旧对象。前述初轮静态检查未覆盖此中断路径。
- 用户已授权修复。实现 Agent `implement_log` 唯一修改 [UtilsLogPlayProbe.cs](../../Tests/EditMode/UtilsLogPlayProbe.cs)，将现有幂等 `DestroyContext` 接入 `AssemblyReloadEvents.beforeAssemblyReload`；主 Agent 负责独立复查及执行记录。真实热重载回归由用户执行，不以验证事件订阅语句的测试代替生命周期验证。
- 修复已落盘：只增加重载前清理事件的去重订阅，复用原有销毁／清空引用逻辑。主 Agent 独立检查确认清理发生在静态引用丢失之前，空对象及重复收尾安全；写入范围、文本格式、文档链接与规范正文保留检查通过。运行时 API、原有测试和元数据未改动。本轮未启动 Unity、编译或执行热重载回归，状态继续为 `InProgress`。
- **新增必要手动回归（待用户验证）**：与上面的两组连续 Play 验证分开执行。记下并临时将 Unity Preferences 的 `Script Changes While Playing` 设为 `Recompile And Continue Playing`；启用探针，进入隔离空场景的 Play，执行 `Emit Exception With Context` 并确认临时上下文对象存在。在 Play 内触发一次脚本重编译，预期重载前旧对象被销毁、重载后无遗留对象；再次执行异常菜单只创建一个新上下文对象，退出 Play 后该对象也被清理，随后 Disarm 不报错。恢复 Preferences 原设置。期间出现的预期异常输出不作为清理失败；若需重新验证配置生命周期，应重新 Arm 后按第 3 项完成两组连续 Play，不能复用被重编译打断的计数。

#### 接口与初始化调整

- 此阶段用户明确要求直接设置开关与等级字段初值、等级字段当时命名为 `Level`、配置方法命名为 `SetLevel`，并删除默认值常量及会话重置方法。主 Agent 与实现 Agent 已对齐相应调用和 Play 验证变更；主 Agent 先同步需求与 Task 的受影响规范，再交实现 Agent 修改运行时和现有测试。当前私有字段名以 2026-09-19 记录为准。
- 新版配置仅随静态字段初始化，关闭 Domain Reload 时保留当前配置；此前“所有新 Play 会话自动恢复默认”的要求及相应验证预期已由本次用户决定替代。首次默认状态仍须在新脚本域且任何配置调用前观测；普通规则测试继续使用明确基线。
- 本次修改已落盘，现有 36 个规则用例与 1 个首次默认用例已迁移调用及参数名断言，Play 探针按两种脚本域配置分别检查字段初始化与配置保留。主 Agent 和独立评审 Agent `review_log` 复查通过；全仓相关源码／文档未发现旧入口引用，写入范围、元数据保留、测试数量、文本格式和链接检查通过。此前重载前清理修复仍在。Unity 编译、用例及新版 Play 行为尚未执行，继续待用户验证并保持 `InProgress`。

### 2026-09-19 测试兼容性修复与运行时整理核查

- 用户报告 `UtilsLogInitialStateTests.cs(7,19)` 的 `CS0246`：无法找到 `NonParallelizableAttribute`。核查当前 Unity Test Framework `1.6.0` 与 Custom NUnit `2.0.5` DLL 后确认该特性不存在，属于此前实现测试时的兼容性漏检。
- 实现 Agent 将两份测试类的 `[TestFixture, NonParallelizable]` 改为 `[TestFixture]`，未新增依赖或替代特性。本地 UTF 的 `CompositeWorkItem.RunChildren` 逐个执行子项；继续按既定条件隔离、串行运行，36 个规则用例与 1 个首次默认用例保持不变。主 Agent 与独立评审 Agent `review_log` 已复查修复；源码引用检索与文本格式检查通过，尚未执行 Unity 编译或测试，需用户重新编译确认错误消除。
- 保留用户的运行时整理：`LogLevel` 已合入 `UtilsLog.cs`，独立 `LogLevel.cs` 已不存在，字段已改为 `_enable`／`_level`。主 Agent 与独立评审对齐后，本 Task 同步产物归属、源码链接及私有字段名；公开 API、初值和既定行为要求保持。
- 另发现当前 `SetLevel` 已删除非法等级校验，与仍有效的需求及 8 个非法等级用例不符。主 Agent 已询问用户是否有意改变这项行为，待决定；收到回复前不修改这项规范、运行时代码或对应测试，也不将当前差异视为验收通过。
- 本轮未启动 Unity 或执行编译；状态保持 `InProgress`。下一步先确认重新编译结果及非法等级处理决定，再完成必要规则与真实 Play 验证（含热重载清理回归），根据结果修复并独立复查，必要验证全部满足后再标为 `Done`。事件总线的接入与联合验收仍由上文链接的事件总线 Task 承担。

### 2026-09-19 模块整体迁移

- 用户明确授权整体迁移，Log 模块移至 `Core/Utils/Log/`，目录及全部子项的原 `.meta`／GUID 随迁移保留；源码、测试与程序集定义内容不变。同步需求、Task 与事件总线入向链接，包 README 仅补充分类目录约定。
- 已核对迁移前后非文档内容一致、元数据完整及文档链接有效；未操作 Git 暂存区，未启动 Unity 或执行导入、编译、测试。状态保持 `InProgress`，上述 `SetLevel` 非法等级处理待决与必要 Unity 验证仍保留。

### 2026-09-19 模块目录改名

- 用户随后选择将模块目录改名为 `Core/Utils/UtilsLog/`，替代上一条迁移记录中的目录名。包标识 `com.liqiangeastsun.gamesdk` 对应本工程实际磁盘目录 `Packages/GameSDK`，本次只改模块目录及对应 `.meta` 文件名；源码、测试、程序集定义和全部 `.meta` 内容／GUID 原样保留，同步产物路径、需求标题及事件总线入向链接。状态仍为 `InProgress`，`SetLevel` 待决保持；未操作 Git 暂存区，未启动 Unity 或执行导入、编译、测试。

### 2026-09-19 用户验证确认

- 用户明确反馈“我已经验证过，请修改状态”。依据本次用户验证确认，将任务更新为 `Done`；本轮 Agent 未运行 Unity，未取得逐项测试结果，不将此反馈扩写为各用例逐项通过。
- 原 `SetLevel` 非法等级校验差异记录保留；本次状态更新不表示该校验已补回，未修改代码、需求或验收正文。
