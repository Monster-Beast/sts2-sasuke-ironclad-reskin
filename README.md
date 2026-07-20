# Sasuke: Ironclad Reskin

《杀戮尖塔 2》战士（Ironclad）的佐助主题**纯视觉美化 Mod**。

> 当前阶段：表现系统、逐卡动画、人物状态机与显示层重构开发。仓库不包含任何从《火影忍者》《杀戮尖塔 2》或其他 Mod 中提取的图片、音频、动画、字体、代码或游戏文件。

## 项目目标

这不是简单换皮，而是一套完整的角色演出包：

- Hebi 时期佐助人物模型、选人立绘、头像和加载页；
- 完整战士卡池的原创佐助主题卡面及 Ancient 输出；
- 卡牌标题可完全重构为佐助招式名称，但内部 `card_id`、规则、升级、存档和联机同步保持原样；
- 24 个以上人物状态/交互动作；
- 每张攻击牌拥有唯一 `animation_id` 和独立成品时间轴；
- 剑术、雷遁、火遁、写轮眼、咒印和蛇术动作原子；
- Quick、Standard、Signature、Cut-in、Finisher 五级战斗演出；
- 营火、商店、胜利、战败 CG 和多人手势；
- Cut-in、粒子、低闪烁、快速战斗和调试设置；
- 保持卡牌数值、结算顺序、随机数和多人同步完全不变；
- 任意资源、命名或 Hook 失败时自动回退原游戏表现。

## 角色版本

v0.1 默认采用疾风传前中期 / Hebi 时期佐助：草薙剑、写轮眼、千鸟系雷遁、火遁、蛇术、咒印和麒麟。轮回眼、天手力、成年披风和完全体须佐能乎不进入默认 Profile。

## 表现目标

| 类别 | 目标 |
|---|---|
| 卡名 | 当前版本完整战士卡池、多人卡与衍生牌的中英文佐助主题显示名 |
| 卡面 | 当前版本战士完整卡池 + Ancient 输出 |
| 人物动作 | 24 个以上状态/交互动作 |
| 攻击动作 | 所有攻击牌逐卡专属时间轴 |
| 演出等级 | Quick / Standard / Signature / Cut-in / Finisher |
| 非战斗立绘 | 选人、加载、营火、商店、胜利、战败等 |
| 设置 | Cut-in、粒子、低闪烁、快速战斗、日志 |
| 性能 | 预加载、锚点缓存、粒子池和三档画质 |
| 兼容 | Stable/Beta、多人、存档和多 Mod |

完整矩阵见 [`docs/design/presentation-matrix.md`](docs/design/presentation-matrix.md)。逐卡动画规则见 [`docs/design/card-specific-animation-system.md`](docs/design/card-specific-animation-system.md)。卡牌改名规则见 [`docs/design/card-renaming-system.md`](docs/design/card-renaming-system.md)。

## 卡牌显示名重构

卡牌改名是纯显示层：

```text
原始 card_id / 规则 / 升级 / 衍生关系
→ card_name_overrides.json
→ 中文或英文佐助显示名
```

首批示例：

| 原卡名 | 佐助显示名 |
|---|---|
| 打击 | 草薙·瞬斩 |
| 痛击 | 写轮眼·破势 |
| 旋风斩 | 千鸟流·剑刃风暴 |
| 恶魔形态 | 咒印·二阶段 |
| 巨石（衍生牌） | 千鸟锐枪 |

标题解析失败时保留原游戏名称；升级牌继续使用 `+` 后缀。命名层不会修改卡牌描述中的伤害、费用、格挡和关键词。

## 当前工程能力

- Godot 4.5.1、.NET 9、BaseLib 和 Harmony 工程；
- JSON 视觉配置加载器与严格校验；
- 卡牌身份优先的 C# 动画选择器；
- 中英文显示名目录与 `CardDisplayNameResolver`；
- 卡内升级、强化、斩杀、快速和低闪烁变体；
- C# `GodotVisualSceneHost` 与原游戏 impact 门控接口；
- 45 个动作与状态原子；
- 5 级演出层级配置；
- 24 个完整表现表面清单；
- 13 张首批卡牌独立灰盒时间轴；
- Demon Form 持久 Form、视觉状态和人物战斗状态机；
- 可运行的佐助几何人物 Rig、程序化 VFX、镜头 Director 和预览场景；
- GitHub Actions 元数据、命名、时间轴和版权边界检查。

## 灰盒预览

在 MegaDot 编辑器中打开并运行：

```text
SasukeIronclad/scenes/runtime/graybox_preview.tscn
```

预览场景只验证人物动作、VFX、镜头和时间轴，不会执行伤害或卡牌逻辑。接入游戏后，Director 会在每个 impact 节点等待原游戏真实命中事件。

## 目录

```text
SasukeIronclad/                 Godot/PCK 场景、脚本、时间轴、命名与视觉配置
SasukeIroncladCode/             C# 选择器、名称解析器、播放服务、Godot 适配器和未来 Hook
docs/character                  佐助与战士角色圣经
docs/design                     卡名、卡面、动画、VFX 和完整表现矩阵
docs/research                   外部项目和官方设定研究
docs/technical                  架构、环境、资源和符号审计
art/                            原创美术源文件与导出目录
animation/                      原创动画源文件、事件表与导出目录
tools/                          仓库校验和测试脚本
```

## 开发环境

- Slay the Spire 2 当前版本；
- .NET SDK 9；
- MegaDot / Godot Mono 4.5.1；
- BaseLib；
- Rider 或 Visual Studio。

修改 `Directory.Build.props` 中的路径：

```xml
<GodotPath>C:/megadot/MegaDot_v4.5.1-stable_mono_win64.exe</GodotPath>
<!-- <Sts2Path>D:/SteamLibrary/steamapps/common/Slay the Spire 2</Sts2Path> -->
```

构建：

```powershell
dotnet restore
dotnet build
dotnet publish
```

不依赖游戏文件的仓库检查：

```bash
python tools/validate_repo.py
python tools/test_card_animation_policy.py
python tools/test_card_name_overrides.py
python tools/test_graybox_timelines.py
python tools/test_demon_form_contract.py
python tools/test_character_state_contract.py
```

## 开发顺序

1. 本地核验当前游戏版本资源、完整卡牌 ID、动画接口和多人状态；
2. 为完整战士普通卡、多人卡和衍生卡建立佐助显示名与卡面语义；
3. 在灰盒预览中完成首批卡牌动作质量；
4. 将名称解析器与 `GodotVisualSceneHost` 接入核验后的游戏视觉 Hook；
5. 替换为正式人物 Rig、卡面、VFX 和非战斗立绘；
6. 完成全部攻击牌逐卡时间轴；
7. 做性能、Stable/Beta 和多人验证。

参见 [`ROADMAP.md`](ROADMAP.md)、[`docs/design/card-renaming-system.md`](docs/design/card-renaming-system.md) 和 [`docs/design/graybox-runtime.md`](docs/design/graybox-runtime.md)。

## 非官方声明

这是免费、非商业的同人 Mod，与 Mega Crit、岸本齐史、集英社、Studio Pierrot、TV Tokyo 或其他权利人没有关联。详细规则见 [`LEGAL.md`](LEGAL.md)。
