# Sasuke: Ironclad Reskin

《杀戮尖塔 2》战士（Ironclad）的佐助主题**纯视觉美化 Mod**。

> 当前阶段：可运行灰盒表现系统、显示名称重构、滚动最新 Beta 审计与精确版本接口接入准备。仓库不包含任何从《火影忍者》《杀戮尖塔 2》或其他 Mod 中提取的图片、音频、动画、字体、代码或游戏文件。

## 不改变游戏效果

本项目只修改玩家看到的表现：

- 原始 `card_id`、费用、伤害、格挡、关键词和规则文本保持不变；
- 升级、生成、复制、变化、存档、随机数和多人同步保持不变；
- 每张牌仍由原始 `card_id` 选择自己的唯一动画；
- 卡牌显示名称可以重构为佐助招式名称，但不会参与游戏逻辑；
- 视觉资源、命名或 Hook 失败时回退原游戏表现。

## 只适配滚动最新 Beta

真实游戏接口只面向 Steam 分支：

```text
public-beta
```

`stable`、历史 Beta build 和手工保留的旧安装不会生成或启用接口 Profile。审计开始前会通过 SteamCMD 查询远端 `public-beta` buildid，并要求本机：

```text
BetaKey == public-beta
本机 buildid == 远端 public-beta buildid
```

完整适配指纹为：

```text
public-beta
+ Steam buildid
+ sts2.dll SHA-256
+ sts2.dll Module MVID
+ BaseLib 版本
+ SteamCMD 远端 Beta Attestation
```

Beta Attestation 当前有效期为 72 小时。仓库一旦出现更高 buildid 的 Profile，旧 Profile 会立即被判定为 `superseded`；即使其时间证明尚未过期，也不能继续安装绑定。

详见 [`docs/technical/latest-beta-policy.md`](docs/technical/latest-beta-policy.md)。

## 角色版本

v0.1 默认采用疾风传前中期 / Hebi 时期佐助：草薙剑、写轮眼、千鸟系雷遁、火遁、蛇术和咒印。轮回眼、天手力、成年披风和完全体须佐能乎不进入默认 Profile。

## 当前完成能力

- Godot 4.5.1、.NET 9、BaseLib 和 Harmony 工程；
- 卡牌身份优先的 C# 动画选择器；
- 内容变体与快速/低闪烁修饰分层，可同时启用；
- 单活动播放句柄、原始 impact 门控和异步失败回退；
- 状态与 Form 独立事务、取消回滚和重复安装恢复；
- Demon Form 咒印二阶段、持久替代待机与显式移除；
- 入场、随机待机、轻/重受击、死亡和胜利人物状态机；
- 剑术、雷遁、火遁、写轮眼、咒印和手里剑程序化 VFX；
- 13 张首批卡牌的独立灰盒时间轴；
- 中英文佐助卡牌显示名、六种标题显示面和卡面生产 Brief；
- 100 次播放/取消压力测试和战斗结束资源释放测试；
- 两次独立本地扫描、卡牌 ID 元数据候选和接口候选审阅工作台；
- 滚动最新 `public-beta` 远端 build 校验；
- 精确 Steam buildid、程序集 SHA-256、MVID、BaseLib 和分支安全门；
- 正式 Binding 仅允许 MethodDef Token，并在安装前再次解析声明类型和签名；
- 未审计版本、过期/旧版 Profile、未审核 Profile 或未注册适配器默认完全禁用。

## 当前 13 张灰盒时间轴

| 原卡牌 | 佐助显示名 | `animation_id` |
|---|---|---|
| Strike | 草薙·瞬斩 | `strike_kusanagi_draw_slash` |
| Defend | 草薙·剑御 | `defend_wire_parry_guard` |
| Bash | 写轮眼·破势 | `bash_sharingan_breaker` |
| Anger | 影手里剑 | `anger_shuriken_afterimage` |
| Cleave | 千鸟流·横扫 | `cleave_chidori_ground_arc` |
| Thunderclap | 千鸟流·雷震 | `thunderclap_chidori_ring_burst` |
| Heavy Blade | 雷遁·草薙断 | `heavy_blade_lightning_execution` |
| Flame Barrier | 火遁·炎阵 | `flame_barrier_uchiha_fire_guard` |
| Whirlwind | 千鸟流·剑刃风暴 | `whirlwind_chidori_blade_storm` |
| Burning Pact | 咒印·献契 | `burning_pact_curse_seal_consumption` |
| Demon Form | 咒印·二阶段 | `demon_form_curse_mark_stage_two` |
| Limit Break | 写轮眼·极限解放 | `limit_break_sharingan_curse_overdrive` |
| Fiend Fire | 火遁·龙火歼灭 | `fiend_fire_dragon_flame_annihilation` |

衍生牌 `GIANT_ROCK` 暂定显示为“千鸟锐枪 / Chidori Spear”，其真实游戏 ID 仍等待当前 Beta 安装版本审计。

## MegaDot 灰盒场景

独立预览：

```text
SasukeIronclad/scenes/runtime/graybox_preview.tscn
SasukeIronclad/scenes/runtime/card_name_preview.tscn
```

自动退出测试：

```text
SasukeIronclad/scenes/runtime/runtime_stress_test.tscn
SasukeIronclad/scenes/runtime/combat_resource_release_test.tscn
SasukeIronclad/scenes/runtime/form_lifecycle_test.tscn
SasukeIronclad/scenes/runtime/character_state_machine_test.tscn
```

GitHub Actions 使用 Godot 4.5.1 headless 实际执行，成功标记为：

```text
STRESS_OK iterations=100 baseline_nodes=37 peak_nodes=37
RELEASE_OK states=0 transients=0
FORM_OK form='' states=0 transients=0
CHAR_STATE_OK state='' terminal=false form=''
```

运行日志不允许出现脚本错误、运行期 `ERROR:` 或 ObjectDB 泄漏警告。

## 最新 Beta 本地审计

审计器只读取托管元数据与文件元数据：

- 不加载或执行 `sts2.dll`；
- 不复制 PCK、DLL、贴图或恢复工程；
- 输出程序集 SHA-256、MVID、Steam buildid、BaseLib 版本；
- 输出候选类型、方法、字段、属性、事件、Metadata Token 和安全的元数据常量；
- 输出候选资源的相对路径、大小、图片尺寸和可选 SHA-256；
- 两次扫描前都会重新确认远端 `public-beta` build；
- 比较一致后生成不选择任何目标的候选审阅表；
- 报告不包含本机游戏安装目录或恢复目录的绝对路径。

准备：

- .NET 9；
- Python 3；
- Valve SteamCMD，或刚刚保存的 `app_info_print 2868840` 输出；
- 已在 Steam 中选择 `public-beta` 并完成更新。

### Windows

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-game-audit.ps1 `
  -GamePath "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  -AssetRoot "D:\STS2-recovered" `
  -Branch public-beta `
  -SteamCmdPath "C:\steamcmd\steamcmd.exe"
```

网络受限时可使用刚保存的 SteamCMD 输出：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-game-audit.ps1 `
  -GamePath "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  -Branch public-beta `
  -SteamCmdOutput ".\steamcmd-app-info.txt"
```

### Linux / macOS

```bash
chmod +x tools/run-game-audit.sh

./tools/run-game-audit.sh \
  --game-path "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2" \
  --asset-root "$HOME/STS2-recovered" \
  --branch public-beta \
  --steamcmd /path/to/steamcmd
```

脚本执行：

```text
远端 public-beta 校验
→ run-1
→ 提示完整启动并退出一次游戏
→ 再次校验远端 public-beta
→ run-2
→ equivalent 比较
→ binding-review.json / binding-review.md
```

结果保存在已被 Git 忽略的 `local-audit/`。候选审阅、MethodDef 约束和 `pending_review` Profile 流程见 [`docs/technical/audit-binding-review.md`](docs/technical/audit-binding-review.md)。

## 游戏绑定安全门

`SasukeIronclad/data/game_integration_contract.json` 当前保持：

```json
"status": "pending_local_audit",
"profiles": []
```

`MainFile` 不再全局执行 `Harmony.PatchAll()`。只有同时满足以下条件才可能进入安装器：

- Contract、Profile 和所需 Binding 均为 `verified`；
- 当前分支为 `public-beta`；
- 当前 build 是 Attestation 记录的远端最新 Beta build；
- Attestation 没有超过 72 小时；
- 当前 Profile 不是仓库中已知较新 Beta Profile 的旧版本；
- Steam buildid、`sts2.dll` SHA-256、MVID 和 BaseLib 完全一致；
- Binding 是 `0x06` MethodDef；
- MethodDef 再次解析出的声明类型和完整签名与审阅结果一致；
- 实际适配器已显式注册。

当前默认适配器会拒绝安装。因此即使配置被误改为 `verified`，也不会安装未经开发和回归的游戏 Hook。任何不匹配、缺失、过期或未验证项都会保留原游戏动画与原标题。

## 自动验证与 Beta 监控

本地核心检查：

```bash
python tools/validate_repo.py
python tools/test_card_animation_policy.py
python tools/test_card_name_overrides.py
python tools/test_card_title_pipeline.py
python tools/test_graybox_timelines.py
python tools/test_demon_form_contract.py
python tools/test_character_state_contract.py
python tools/test_runtime_safety_contract.py
python tools/test_game_integration_contract.py
python tools/test_game_integration_startup.py
python tools/test_latest_beta_guard.py
python tools/test_latest_beta_profiles.py
python tools/test_audit_review_pipeline.py
```

GitHub Actions 还会：

- 使用 Godot.NET.Sdk 4.5.1 / .NET 9 编译 Runtime、Adapters 和 Visuals；
- 使用 Godot 4.5.1 headless 导入全部 `.gd`、`.tscn`；
- 真实执行四个运行时测试场景；
- 编译并执行审计器、精确版本安全门和 MethodDef 解析矩阵；
- 解析 Bash 与 PowerShell 审计脚本；
- 生成合成 Beta Attestation 与不自动选择候选的审阅表；
- 每 6 小时通过 SteamCMD 查询一次远端 `public-beta`；
- 当 Contract 启用后，若没有且仅有一个匹配远端 build 的有效 Profile，监控工作流失败；
- 发现旧 verified Profile、重复当前 Profile 或超前于远端的 Profile 时失败；
- 将脚本错误、运行期 `ERROR:` 和 ObjectDB 泄漏视为失败。

## 目录

```text
SasukeIronclad/                 Godot 场景、脚本、时间轴和视觉配置
SasukeIroncladCode/             C# 选择器、播放服务、精确版本安全门和未来 Hook
SasukeIronclad/data/            卡牌配置、标题、Beta 策略和集成契约
docs/design                     动画、卡面、命名和完整表现矩阵
docs/technical                  架构、资源、接口审计和最新 Beta 流程
art/                            原创美术源文件与导出目录
animation/                      原创动画源文件、事件表与导出目录
tools/game_audit                当前安装版本的元数据审计 CLI
tools/                          仓库校验、Godot/.NET 测试和 Beta 工具
```

## 本地开发环境

- Slay the Spire 2 当前 `public-beta`；
- .NET SDK 9；
- MegaDot / Godot Mono 4.5.1；
- BaseLib；
- Python 3；
- SteamCMD；
- Rider 或 Visual Studio。

修改 `Directory.Build.props` 中的路径：

```xml
<GodotPath>C:/megadot/MegaDot_v4.5.1-stable_mono_win64.exe</GodotPath>
<!-- <Sts2Path>D:/SteamLibrary/steamapps/common/Slay the Spire 2</Sts2Path> -->
```

完整游戏工程构建：

```powershell
dotnet restore
dotnet build
dotnet publish
```

## 仍需完成

1. 在 Steam 当前最新 `public-beta` 执行两次真实审计；
2. 核验完整 Ironclad 卡牌 ID、资源路径与人物节点；
3. 观察并审阅 impact、状态移除、战斗结束和六种标题 UI 方法；
4. 为该精确 Beta build 实现实际适配器，并通过 MethodDef 二次解析；
5. 完成真实游戏、回退、多人和多 Mod 回归后，人工晋级 Profile；
6. 每次 Beta 更新后重复审计并立即废止旧 Profile；
7. 扩展完整卡池名称、卡面和专属动画；
8. 替换正式原创人物 Rig、卡面、VFX 和非战斗立绘。

参见 [`ROADMAP.md`](ROADMAP.md)、[`docs/design/card-renaming-system.md`](docs/design/card-renaming-system.md)、[`docs/design/graybox-runtime.md`](docs/design/graybox-runtime.md)、[`docs/technical/audit-binding-review.md`](docs/technical/audit-binding-review.md) 和 [`docs/technical/latest-beta-policy.md`](docs/technical/latest-beta-policy.md)。

## 非官方声明

这是免费、非商业的同人 Mod，与 Mega Crit、岸本齐史、集英社、Studio Pierrot、TV Tokyo 或其他权利人没有关联。详细规则见 [`LEGAL.md`](LEGAL.md)。
