# Sasuke: Ironclad Reskin

《杀戮尖塔 2》战士（Ironclad）的佐助主题**纯视觉美化 Mod**。

> 当前阶段：表现系统和垂直切片开发。仓库不包含任何从《火影忍者》《杀戮尖塔 2》或其他 Mod 中提取的图片、音频、动画、字体、代码或游戏文件。

## 项目目标

这不是简单换皮，而是一套完整的角色演出包：

- Hebi 时期佐助人物模型、选人立绘、头像和加载页；
- 完整战士卡池的原创佐助主题卡面及 Ancient 输出；
- 24 个以上人物状态/交互动作；
- 24 个以上剑术、雷遁、火遁、写轮眼、咒印和蛇术动作 Profile；
- Quick、Standard、Signature、Cut-in、Finisher 五级战斗演出；
- 营火、商店、胜利、战败 CG 和多人手势；
- Cut-in、粒子、低闪烁、快速战斗和调试设置；
- 保持卡牌数值、结算顺序、随机数和多人同步完全不变；
- 任意资源或 Hook 失败时自动回退原游戏表现。

## 角色版本

v0.1 默认采用疾风传前中期 / Hebi 时期佐助：草薙剑、写轮眼、千鸟系雷遁、火遁、蛇术、咒印和麒麟。轮回眼、天手力、成年披风和完全体须佐能乎不进入默认 Profile。

## 表现目标

| 类别 | 目标 |
|---|---|
| 卡面 | 当前版本战士完整卡池 + Ancient 输出 |
| 人物动作 | 24 个以上状态/交互动作 |
| 攻击动作 | 24 个以上可复用 Profile |
| 演出等级 | Quick / Standard / Signature / Cut-in / Finisher |
| 非战斗立绘 | 选人、加载、营火、商店、胜利、战败等 |
| 设置 | Cut-in、粒子、低闪烁、快速战斗、日志 |
| 性能 | 预加载、锚点缓存、粒子池和三档画质 |
| 兼容 | Stable/Beta、多人、存档和多 Mod |

完整矩阵见 [`docs/design/presentation-matrix.md`](docs/design/presentation-matrix.md)。参考项目分析见 [`docs/research/reference-chizuru-ironclad.md`](docs/research/reference-chizuru-ironclad.md)。

## 当前工程能力

- Godot 4.5.1、.NET 9、BaseLib 和 Harmony 工程；
- JSON 视觉配置加载器与严格校验；
- 卡牌语义映射；
- 45 个动作与状态 Profile；
- 5 级演出层级配置；
- 24 个完整表现表面清单；
- GitHub Actions 元数据和版权边界检查。

## 目录

```text
SasukeIronclad/                 Godot/PCK 资源、动作层级和表现表面配置
SasukeIroncladCode/             C# 配置注册和未来视觉 Hook
/docs/character                 佐助与战士角色圣经
/docs/design                    卡面、动画、VFX 和完整表现矩阵
/docs/research                  外部项目和官方设定研究
/docs/technical                 架构、环境、资源和符号审计
art/                            原创美术源文件与导出目录
animation/                      原创动画源文件、事件表与导出目录
tools/                          仓库校验和任务脚本
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
```

## 开发顺序

1. 本地核验当前游戏版本资源、卡牌 ID、动画接口和多人状态；
2. 完成从选人界面到战斗结束的垂直切片；
3. 实现五级动作路由和表现设置；
4. 扩展完整人物动作和非战斗立绘；
5. 完成全部卡面和 Ancient 输出；
6. 做性能、Stable/Beta 和多人验证。

参见 [`ROADMAP.md`](ROADMAP.md)。

## 非官方声明

这是免费、非商业的同人 Mod，与 Mega Crit、岸本齐史、集英社、Studio Pierrot、TV Tokyo 或其他权利人没有关联。详细规则见 [`LEGAL.md`](LEGAL.md)。
