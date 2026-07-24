# 游戏集成状态

## 当前结论

真实游戏接口仍然保持关闭：

```json
{
  "contract_status": "pending_local_audit",
  "verified_profiles": 0,
  "default_installer": "reject",
  "unconditional_patch_all": false
}
```

本文件用于区分三类已经完成的开发成果：

1. 灰盒视觉运行时；
2. 本地元数据审计与人工候选复核；
3. 精确版本启动安全门与 MethodDef 二次解析。

它不代表已完成真实游戏 Hook。

## 已完成

### 灰盒视觉运行时

- 13 张卡牌拥有唯一时间轴；
- 原始 impact 门控与动态多段；
- State、Form、人物状态、Cut-in 和低闪烁；
- 100 次压力、释放、Form 和人物状态场景测试；
- Godot 4.5.1 headless 与 .NET 9 CI。

### 本地审计与人工复核

- 双次扫描和 `equivalent` 比较；
- 类型、方法、字段、属性、事件和常量元数据；
- 卡牌 ID 候选；
- 六类视觉与六类标题候选排名；
- 不自动选择目标；
- 证据不完整时拒绝编译 Profile；
- Profile 编译结果强制为 `pending_review`。

### 启动安全门

- 无条件 `Harmony.PatchAll()` 已移除；
- 精确匹配 Steam buildid、程序集 SHA-256/MVID、BaseLib 和分支；
- 自动读取 Steam BetaKey；
- 自动查找本地 Mods 和 Workshop BaseLib；
- Contract、Profile、Binding 三层 verified；
- 正式 Binding 只能是 `0x06` MethodDef；
- 安装前再次解析 Token、MVID、声明类型和签名；
- 默认 installer 拒绝安装；
- 安装失败时 fail closed。

## 尚未完成

- 真实 Stable/Beta 审计结果；
- 完整 Ironclad card_id；
- 人物场景和节点；
- 卡牌视觉请求；
- 权威 impact；
- 状态/Form 移除和战斗结束；
- 六种标题 UI 方法；
- 实际 Harmony 适配器；
- 真实游戏、多人与多 Mod 回归。

## 下一次输入

下一开发步骤需要以下纯元数据文件：

```text
local-audit/run-1/audit-report.json
local-audit/run-2/audit-report.json
local-audit/comparison/audit-comparison.json
local-audit/review/binding-review.json
```

在这些文件经过人工观察和复核前，不会增加猜测的游戏 Hook。
