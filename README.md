# Sasuke: Ironclad Reskin

《杀戮尖塔 2》战士（Ironclad）的佐助主题**纯视觉美化 Mod**。

> 当前阶段：可运行灰盒表现系统、显示名称重构与游戏接口审计。仓库不包含任何从《火影忍者》《杀戮尖塔 2》或其他 Mod 中提取的图片、音频、动画、字体、代码或游戏文件。

## 不改变游戏效果

本项目只修改玩家看到的表现：

- 原始 `card_id`、费用、伤害、格挡、关键词和规则文本保持不变；
- 升级、生成、复制、变化、存档、随机数和多人同步保持不变；
- 每张牌仍由原始 `card_id` 选择自己的唯一动画；
- 卡牌显示名称可以重构为佐助招式名称，但不会参与游戏逻辑；
- 视觉资源、命名或 Hook 失败时回退原游戏表现。

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
- 不执行游戏程序集的本地版本、资源与方法签名审计工具；
- 精确 build 指纹安全门：没有已验证 Profile 时所有真实游戏绑定保持关闭。

## 当前 13 张灰盒时间轴

| 原卡牌 | 佐助显示名 | animation_id |
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

衍生牌 `GIANT_ROCK` 暂定显示为“千鸟锐枪 / Chidori Spear”，其真实游戏 ID 仍等待安装版本审计。

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

已在 GitHub Actions 使用官方 Godot 4.5.1 headless 实际运行，成功标记为：

```text
STRESS_OK iterations=100 baseline_nodes=37 peak_nodes=37
RELEASE_OK states=0 transients=0
FORM_OK form='' states=0 transients=0
CHAR_STATE_OK state='' terminal=false form=''
```

运行日志中不允许出现脚本错误、运行期 `ERROR:` 或 ObjectDB 泄漏警告。

## 本地游戏审计

审计工具位于 `tools/game_audit`，只读取托管元数据和文件元数据：

- 不加载或执行 `sts2.dll`；
- 不复制 PCK、DLL、贴图或恢复工程；
- 输出程序集 SHA-256、MVID、Steam buildid、BaseLib 版本；
- 输出候选类型/方法签名、Metadata Token；
- 输出候选资源的相对路径、大小、图片尺寸和可选 SHA-256；
- 自动比较两次独立运行，避免把一次性或错误结果当成已验证接口；
- 报告不会包含本机游戏安装目录或恢复目录的绝对路径。

Windows：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-game-audit.ps1 `
  -GamePath "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  -AssetRoot "D:\STS2-recovered" `
  -Branch stable
```

Linux / macOS：

```bash
chmod +x tools/run-game-audit.sh
./tools/run-game-audit.sh \
  "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2" \
  "$HOME/STS2-recovered"
```

结果写入已被 Git 忽略的 `local-audit/`。详细规则与填写表见 [`docs/technical/local-asset-audit.md`](docs/technical/local-asset-audit.md)。

## 游戏绑定安全门

`SasukeIronclad/data/game_integration_contract.json` 当前保持：

```json
"status": "pending_local_audit",
"profiles": []
```

因此现在不会绑定任何猜测的 Harmony 目标。只有某个 Profile 同时精确匹配以下信息，且全部必需视觉事件与六种标题显示面均标记为 `verified`，运行时才允许启用对应适配器：

```text
branch + Steam buildid + sts2.dll SHA-256 + Module MVID + BaseLib version
```

任何不匹配、缺失或未验证项都会保留原游戏动画与原标题。

## 自动验证

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
```

GitHub Actions 还会：

- 使用独立 Godot.NET.Sdk 4.5.1 / .NET 9 工程编译 Runtime、Adapters 和 Visuals 层；
- 使用 Godot 4.5.1 headless 导入全部 `.gd`、`.tscn`；
- 真实执行四个运行时测试场景；
- 编译并运行本地游戏审计工具的双扫描脱敏自测；
- 将解析、编译和场景日志保存为 Artifact；
- 将脚本错误、运行期 `ERROR:` 和 ObjectDB 泄漏视为失败。

## 目录

```text
SasukeIronclad/                 Godot 场景、脚本、时间轴和视觉配置
SasukeIroncladCode/             C# 选择器、播放服务、Godot 适配器和未来 Hook
SasukeIronclad/data/            卡牌动画、显示名、标题显示面、卡面 Brief 和集成契约
docs/design                     动画、卡面、命名和完整表现矩阵
docs/research                   外部项目和官方设定研究
docs/technical                  架构、环境、资源和符号审计
art/                            原创美术源文件与导出目录
animation/                      原创动画源文件、事件表与导出目录
tools/game_audit                当前安装版本的元数据审计 CLI
tools/                          仓库校验、Godot 和 .NET 契约测试
```

## 本地开发环境

- Slay the Spire 2 当前安装版本；
- .NET SDK 9；
- MegaDot / Godot Mono 4.5.1；
- BaseLib；
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

1. 在当前 Stable/Beta 安装版本执行两次本地审计；
2. 核验原始 card_id、人物节点、命中、状态移除、战斗结束和卡牌标题 UI；
3. 将确认结果手工写入集成 Profile，只对精确 build 启用绑定；
4. 扩展完整 Ironclad 卡池名称、卡面和逐卡动画；
5. 替换正式原创人物 Rig、卡面、VFX 和非战斗立绘；
6. 完成真实游戏、多人和多 Mod 验证。

参见 [`ROADMAP.md`](ROADMAP.md)、[`docs/design/card-renaming-system.md`](docs/design/card-renaming-system.md) 和 [`docs/design/graybox-runtime.md`](docs/design/graybox-runtime.md)。

## 非官方声明

这是免费、非商业的同人 Mod，与 Mega Crit、岸本齐史、集英社、Studio Pierrot、TV Tokyo 或其他权利人没有关联。详细规则见 [`LEGAL.md`](LEGAL.md)。
