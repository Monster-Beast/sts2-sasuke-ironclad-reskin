# Sasuke: Ironclad Reskin

《杀戮尖塔 2》战士（Ironclad）的佐助主题**纯视觉美化 Mod**。

> 当前阶段：工程骨架与视觉系统开发。仓库不包含任何从《火影忍者》或《杀戮尖塔 2》中提取的图片、音频、动画、字体或游戏文件。

## 目标

- 将战士人物模型、头像和选择界面统一为 Hebi 时期佐助主题；
- 重绘战士卡牌插画；
- 为卡牌建立剑术、雷遁、火遁、写轮眼与咒印动作 Profile；
- 保持卡牌数值、结算顺序、随机数和多人同步完全不变；
- 任意视觉资源加载失败时自动回退原游戏表现。

## 角色版本

v0.1 默认采用疾风传前中期 / Hebi 时期佐助：草薙剑、写轮眼、千鸟系雷遁、火遁与咒印。轮回眼、天手力、成年披风和完全体须佐能乎不进入默认 Profile。

## 已完成

- 基于当前社区模板的 Godot 4.5.1、.NET 9、BaseLib 和 Harmony 工程；
- 可运行的 JSON 视觉配置加载器与严格校验；
- 13 张首批卡牌概念和 13 个动作 Profile；
- 佐助与战士角色设定、卡面规范、动画/VFX 规范；
- GitHub Actions 元数据校验；
- 版权与原始资源隔离规则。

## 目录

```text
SasukeIronclad/                 Godot/PCK 资源与视觉配置
SasukeIroncladCode/             C# 初始化、配置注册和未来视觉 Hook
docs/                           角色、美术、动画、技术文档
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

1. 本地核验当前游戏版本的战士人物、卡图和动画路径；
2. 完成 Strike、Defend、Bash 三张卡面；
3. 接入基础待机、快斩和格挡；
4. 定位稳定的纯视觉播放 Hook；
5. 扩展到完整战士卡池和人物动作。

参见 [ROADMAP.md](ROADMAP.md) 和 [docs/technical/local-asset-audit.md](docs/technical/local-asset-audit.md)。

## 非官方声明

这是免费、非商业的同人 Mod，与 Mega Crit、岸本齐史、集英社、Studio Pierrot、TV Tokyo 或其他权利人没有关联。详细规则见 [LEGAL.md](LEGAL.md)。
