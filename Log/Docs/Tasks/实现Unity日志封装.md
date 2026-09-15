# 实现 Unity 日志封装

状态：`NotStarted`。

## 产物与前置

按[需求](../需求.md)交付 `Log/Log.cs`、`Log/LogLevel.cs`、`Log/Tests/` 测试及元数据，均相对包根。测试使用独立程序集；运行时复用 `GameSDK.asmdef`，由[普通类单例任务](../../../Singleton/Docs/Tasks/实现普通类单例.md)提供或移交写入权，不依赖单例类型。

## 待实施 API

命名空间为 `GameSDK`，定义 `public enum LogLevel { Info = 0, Warning = 1, Error = 2 }` 和 `public static class Log`。下列方法均为 `public static`：

| 配置与查询 | 输出 |
|---|---|
| `void SetEnabled(bool enabled)` | `void Info(object message, UnityEngine.Object context = null)` |
| `void SetMinimumLevel(LogLevel minimumLevel)` | `void Warning(object message, UnityEngine.Object context = null)` |
| `bool GetEnabled()` | `void Error(object message, UnityEngine.Object context = null)` |
| `LogLevel GetMinimumLevel()` | `void Exception(System.Exception exception, UnityEngine.Object context = null)` |
| `bool IsEnabled(LogLevel level)` | |

## 实施要点

1. `Log` 持有开关和阈值，先校验非法等级或空异常，再读写状态或输出；比较本模块等级，不比较 Unity `LogType` 数值。
2. 借鉴 [Unity Logger](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Logging/Logger.cs) 的过滤后转换、[Serilog](https://github.com/serilog/serilog/blob/dev/src/Serilog/Core/Logger.cs) 的 `IsEnabled` 查询：昂贵消息每次先查询再构造，输出内部复查。保留运行时切换，不加 `Conditional`、仅 Editor 条件或第三方依赖。
3. 静态初值及带 `RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)` 的重置方法统一恢复默认；宿主在重置后设置配置。

## 必要验证与交接

测试前保存 Log 与 Unity 配置，串行执行，在 `finally` / teardown 恢复配置及临时处理器；普通消息按处理器收到的转换后内容观测。

- **静态检查**：API、程序集隔离与元数据完整；过滤先于转换和转发，原消息交给 Debug，运行时代码不改 Unity 全局设置，各构建类型保留调用路径。
- **EditMode 规则测试**：覆盖矩阵、默认值、反复修改与重新开启、非法输入及 getter；查询须逐项符合矩阵并立即反映修改。用计数验证不补发、被过滤时无 `ToString()`、查询 guard 跳过昂贵构造；查询后关闭开关再输出，仍须过滤。
- **输出与失败测试**：观测 Unity 类型、内容（含 `null`、空字符串）、上下文及原异常；处理器抛错须原样传播且无递归报告。分别改变包装层、Unity 全局过滤和 Console 可见性，核对转发及其他直接 Unity 日志，不能只凭 Console 无记录判断过滤。
- **真实 Unity / Play 验证**：确认异常类型、消息、原始抛出位置及对象定位；分别启用和关闭 Domain Reload，修改配置后再次 Play，应恢复默认且接受宿主随后配置。

Unity 导入、编译及测试由用户执行，Agent 提供步骤；必要项未完成时注明待用户验证，不标 `Done`。交付记录实际结果与独立审查，将稳定 API 迁入[模块 README](../../README.md)，本文改为引用。[事件总线任务](../../../EventBus/Docs/Tasks/实现事件总线.md)负责接入 `Log.Exception`、报告失败捕获、重入保护、独立兜底及联合验收。
