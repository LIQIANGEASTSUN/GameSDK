# 实现 MonoBehaviour 单例

状态：`InProgress`；代码实施已授权，Unity 验证待用户执行。

实现及源码／测试唯一写入者：`implement_mono_singleton`；独立检查：主 Agent 与 `review_task_readiness`；共享文档与最终整合：主 Agent。

## 交付与范围

- 实现组件单例，以 PlayMode 验证[需求](../需求.md)全部 AC。
- 写入 `Singleton/MonoSingleton.cs`、`Singleton/Tests/PlayMode/` 及必要元数据，测试程序集独立。仅依赖[普通类任务](实现普通类单例.md)的包运行时程序集；共享文件交原写入者串行调整，模块文档由[负责人](../状态.md)整合。

## 待实施契约

命名空间 `GameSDK`，基类 `public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>`。`Instance`、`ReleaseInstance()`、`OnInitialize()`、`OnRelease()` 的签名及具体类型／构造约束见[普通类契约](实现普通类单例.md#待实施契约)；创建由 Unity 负责，构造及字段初始化不访问 Unity API、不取得需回收资源。

运行期主线程访问，不加线程同步；独立实现组件，状态、忙碌调用、钩子次数与异常语义沿用[普通类生命周期](实现普通类单例.md#生命周期实现)。框架仅占用 `Awake`、`OnDestroy`，派生类不得遮蔽。禁用不释放，不调用 `DontDestroyOnLoad`，不限制宿主层级。

## Unity 生命周期实现

1. **门禁**：`Instance` 先检查 `Application.isPlaying` 及 [`Application.exitCancellationToken.IsCancellationRequested`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-exitCancellationToken.html)，非运行或退出门禁生效时抛 `InvalidOperationException`，不返回已有实例；`ReleaseInstance` 仍可清理。引擎全局退出状态覆盖未访问过的类型，不建自有会话机制。
2. **已有组件**：`Ready` 且组件有效则返回；无有效登记时，从已加载场景查找含非活动者的组件，排除已释放及框架标记待销毁者，按 InstanceID 升序选取。其余候选按重复组件退役，不初始化、不删宿主。选中者进入 `Initializing`，显式初始化，不改变激活状态。
3. **缺失时创建**：先进入 `Initializing`，新建 GameObject 并设为非活动，再 `AddComponent<T>`；登记候选及自建宿主所有权后调用 `OnInitialize`，成功后激活宿主。初始化与激活期间保持 `Initializing`，公共获取／释放遵循忙碌语义。两种候选发布 `Ready` 并返回前，均再次检查运行／退出门禁和组件有效性；创建失败、组件无效或门禁生效均失败收尾，不遗留自建宿主。
4. **Awake**：依次判断：本实例已退役／框架标记待销毁 → 返回，不登记；本实例为 `Ready`／当前 `Initializing` 候选 → 返回，不重复初始化；`Empty` → 通过相同门禁才登记并初始化，否则不初始化、按原生入口报错；其他实例占据初始化／就绪／释放状态 → 本组件按重复者退役。已登记者优先，不另扫全场景。
5. **释放**：先撤销获取资格、永久退役，再执行至多一次 `OnRelease`。`finally` 仅在登记仍属本实例时清除并回 `Empty`，再安排销毁：框架自建宿主销毁整个宿主，预置宿主仅销毁本组件。重复者仅移除本组件、不调用两钩子；待销毁者不得重新成为候选。
6. **OnDestroy 与异常**：按实例清理，不调用静态 `ReleaseInstance`；已释放／未初始化者不重复清理，旧回调不影响新登记。初始化中销毁只记失效，由当前初始化流程失败收尾，避免钩子交叠。显式入口收尾后抛错，`Awake`／`OnDestroy` 收尾后用 `Debug.LogException` 报告，不依靠 Unity 回滚。

获取／释放发现登记的托管引用仍在而 Unity 对象失效时，先清理旧实例剩余资源一次、撤销登记；清理报错则本次抛出，下次获取才重建。[从未激活者不保证收到 OnDestroy](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnDestroy.html)，须遵守需求的先释放约定；遗漏时仅下次访问才检测，无法保证及时清理或访问已销毁的 Unity 资源。不以主动激活预置宿主或轮询弥补。

## 验证

静态检查构造、框架消息及程序集隔离。PlayMode 测试使用真实组件与钩子计数，覆盖[普通类验证](实现普通类单例.md#验证)中适用的类型隔离、失败、重入、幂等场景，以及：

| 触发 | 预期 |
|---|---|
| 获取已有活动／非活动组件；无组件时获取 | 复用且不改变激活状态；缺失只建一个宿主与组件，初始化后才返回。 |
| 多候选；自建宿主激活触发 Awake；初始化中再获取 | 重复者不执行钩子、保留宿主；本实例初始化一次，公共重入抛错、不发布半成品。 |
| 初始化取得部分资源后报错；初始化／激活中对象失效 | 撤销登记、清理一次、无自建宿主遗留；初始化与清理异常均报告，之后可重试。 |
| 主动释放后同帧再获取，随后旧 OnDestroy 到达 | 新实例有效；旧组件不参与候选、不重复清理、不移除新登记。 |
| 已释放的非活动旧组件在实际销毁前被激活 | 不再初始化或登记，不影响新实例。 |
| 外部销毁已激活实例；从未激活者先释放再销毁；遗漏释放后再获取 | 前两者清理一次；遗漏者下次访问补清理，失败则先报错、不创建，后续才重试。 |
| 释放自建／预置实例；禁用、切场景，使用者自行保留对象 | 按宿主所有权销毁，不误删使用者宿主及其他组件；禁用不释放，场景保留由使用者决定。 |
| 编辑模式获取；真实退出回调首次获取未用过的类型 | 拒绝获取，无新增或初始化的 Unity 对象；不以修改私有标记代替真实退出验证。 |

提供入口与预期，由用户执行 Unity 编译、PlayMode 测试及停止 Play 的真实退出场景，反馈前不标 `Done`。记录实际结果后交负责人联合验收；交付时将稳定 API、使用示例迁入[模块 README](../../README.md)，本文改为引用。
