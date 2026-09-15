# EventBus

为上层业务、GameHub 与 GameSDK 模块提供基于字符串事件名、支持 0～3 个类型化参数的事件通知。公开 API 与实现均归 GameSDK；依赖普通类单例与日志输出能力，包边界见 [GameSDK](../README.md)。

- [需求与验收](Docs/需求.md)
- [模块状态与前置条件](Docs/状态.md)
- [实现事件总线](Docs/Tasks/实现事件总线.md)
