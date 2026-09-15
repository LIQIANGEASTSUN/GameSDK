# GameSDK

以下为设计约定；目前仅有包骨架与模块规划，尚无可调用 API，包间依赖与程序集尚未配置。

## 目的与职责

- 封装第三方登录、广告、支付等服务，以及资源管理、热更新等框架所需的第三方能力，屏蔽供应商与实现库差异。
- 对外提供稳定能力，内部承担厂商接入、专属配置与初始化、API 适配；不向消费者泄露厂商类型，也不承载具体游戏的业务与数据规则。
- Singleton 作为本包的基础模块，负责普通类与 MonoBehaviour 单例的受控创建及生命周期。
- EventBus 作为本包的基础模块，为上层、Hub 与 SDK 自身模块提供事件通知。
- Log 作为本包的基础模块，封装 Unity 日志，通过静态入口统一控制自身日志的开关与输出级别。

## 依赖与程序集

- 依赖 [GameInterface](https://github.com/LIQIANGEASTSUN/GameInterface) 及适配所需的第三方库，不反向依赖 GameHub 或宿主项目实现，不形成循环。
- 自有运行时代码由一个程序集承载，按功能目录与命名空间组织，不逐功能拆分 asmdef；Editor、Tests 与运行时隔离，不合并第三方程序集。

## 接入约定

- 第三方能力的接入方通过通用契约消费公开稳定能力；厂商实现的选择、创建与适配留在本包内部。Singleton 按自身派生与生命周期约定接入。
- EventBus 按用户要求提供直接单例入口，允许引用 GameSDK 的消费者与本包模块按其契约使用同一事件总线。
- Log 按用户要求采用静态类与静态方法，允许引用 GameSDK 的消费者及本包模块直接调用，不引入单例或额外接口层。
- 具体 API、配置、生命周期与用法随实现维护在源码旁 README；与其他包的组合约定见 [GameHub](https://github.com/LIQIANGEASTSUN/GameHub#组合使用)。

## 模块入口

- [Singleton](Singleton/README.md)：需求与实施任务已规划，尚未实现。
- [EventBus](EventBus/README.md)：事件通知的需求、前置条件与实施任务。
- [Log](Log/README.md)：Unity 日志封装的需求、当前状态与实施任务。
