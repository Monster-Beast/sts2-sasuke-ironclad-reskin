# 当前安装版本本地资源与符号审计

> 本文只记录路径、尺寸、格式、哈希、元数据 Token 和方法签名。禁止提交 PCK、DLL、贴图、动画、音频、字体、恢复工程或反编译源码。

## 审计状态

- 游戏分支：待填写（stable / beta）
- Steam buildid：待填写
- `sts2.dll` SHA-256：待填写
- `sts2.dll` MVID：待填写
- BaseLib 版本：待填写
- BaseLib 清单 SHA-256：待填写
- MegaDot：4.5.1（本机实际版本待填写）
- 操作系统与架构：待填写
- 第一次审计时间：待填写
- 第二次审计时间：待填写
- 两次结果一致：待填写

## 自动审计工具

仓库提供 `tools/game_audit`。工具使用 `System.Reflection.Metadata` / `PEReader` 读取托管元数据，不加载或执行 `sts2.dll`；资源扫描只输出相对路径、文件大小、图片尺寸和 SHA-256，不复制源文件。

### Windows 一键执行两次审计

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-game-audit.ps1 `
  -GamePath "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  -AssetRoot "D:\STS2-recovered" `
  -Branch stable
```

`-AssetRoot` 可省略；省略后只能扫描游戏安装树中可见的文件，无法获得 PCK 内部资源路径。

### Linux / macOS

```bash
chmod +x tools/run-game-audit.sh
./tools/run-game-audit.sh \
  "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2" \
  "$HOME/STS2-recovered"
```

### 手工单次扫描

```powershell
dotnet run --project tools/game_audit/SasukeIronclad.GameAudit.csproj -- scan `
  --game-path "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  --asset-root "D:\STS2-recovered" `
  --output "local-audit\run-1" `
  --branch stable `
  --session run-1 `
  --megadot-version 4.5.1
```

### 比较两次独立结果

```powershell
dotnet run --project tools/game_audit/SasukeIronclad.GameAudit.csproj -- compare `
  --first "local-audit\run-1\audit-report.json" `
  --second "local-audit\run-2\audit-report.json" `
  --output "local-audit\comparison"
```

只有 `audit-comparison.json` 中 `equivalent=true` 时，才能把候选项标记为“两次独立启动确认”。

## 工具输出

每次扫描生成：

```text
local-audit/run-N/
├─ audit-report.json       完整机器可读元数据
├─ audit-report.md         便于人工检查的表格
└─ audit-fingerprint.json  用于两次结果比较的稳定指纹
```

比较生成：

```text
local-audit/comparison/
├─ audit-comparison.json
└─ audit-comparison.md
```

仓库已经忽略 `local-audit/` 和 `audit-output/`。这些目录不得提交；确认后的少量结果手工整理到本文。

## 自动候选不等于已验证 Hook

工具输出的是候选符号和候选资源，不会自动创建 Harmony Patch。一个 Hook 只有同时满足以下条件才可标记为 `VERIFIED`：

1. 记录完整类型名、方法名、参数类型、返回类型和 Metadata Token；
2. `sts2.dll` SHA-256、MVID、Steam buildid 与报告一致；
3. 两次独立游戏启动后的审计结果一致；
4. 在 Stable/Beta 中分别记录，禁止跨版本复用签名；
5. 已确认该点只读取或触发视觉，不修改伤害、费用、随机数或行动队列；
6. 失败时能够保留原游戏表现；
7. 多人模式下确认只创建本地视觉节点。

## 人物与静态资源

| 项目 | 相对资源路径 | 类型/尺寸 | SHA-256 | 两次确认 | 状态 |
|---|---|---|---|---|---|
| 战斗人物模型/场景 | 待核验 | 待核验 | 待核验 | 否 | TODO |
| 人物脚底锚点 | 待核验 | NodePath 待核验 | - | 否 | TODO |
| 头像 | 待核验 | 待核验 | 待核验 | 否 | TODO |
| 选人立绘 | 待核验 | 待核验 | 待核验 | 否 | TODO |
| 地图/顶部头像 | 待核验 | 待核验 | 待核验 | 否 | TODO |
| Strike 卡图 | 待核验 | 待核验 | 待核验 | 否 | TODO |
| Defend 卡图 | 待核验 | 待核验 | 待核验 | 否 | TODO |
| Bash 卡图 | 待核验 | 待核验 | 待核验 | 否 | TODO |
| 战士完整卡图目录 | 待核验 | 待核验 | 目录清单指纹待核验 | 否 | TODO |
| 卡牌标题字体/Label | 待核验 | Node/Theme 待核验 | 待核验 | 否 | TODO |

## 人物动画与事件

| 目的 | 场景/动画名 | 原始事件或时刻 | 恢复/回退 | 两次确认 | 状态 |
|---|---|---|---|---|---|
| 待机 | 待核验 | 待核验 | 原待机 | 否 | TODO |
| 普通攻击 | 待核验 | impact 待核验 | 原攻击动画 | 否 | TODO |
| 格挡 | 待核验 | block 视觉事件待核验 | 原格挡动画 | 否 | TODO |
| 轻受击 | 待核验 | 待核验 | 原受击动画 | 否 | TODO |
| 重受击 | 待核验 | 待核验 | 原受击动画 | 否 | TODO |
| 死亡 | 待核验 | 战斗终态待核验 | 原死亡动画 | 否 | TODO |
| 胜利 | 待核验 | 战斗终态待核验 | 原胜利动画 | 否 | TODO |

## 托管 Hook 候选与最终签名

| 目的 | 完整类型 | 方法签名 | Token | 适用 build/MVID | 回退 | 两次确认 | 状态 |
|---|---|---|---|---|---|---|---|
| 卡牌视觉请求 | 待核验 | 待核验 | 待核验 | 待核验 | 保留原动画 | 否 | TODO |
| 单段 impact | 待核验 | 待核验 | 待核验 | 待核验 | 不播放 Mod impact | 否 | TODO |
| 多段 impact index | 待核验 | 待核验 | 待核验 | 待核验 | 不播放 Mod impact-loop | 否 | TODO |
| 状态安装/移除通知 | 待核验 | 待核验 | 待核验 | 待核验 | 清理本地状态 | 否 | TODO |
| Form/能力移除通知 | 待核验 | 待核验 | 待核验 | 待核验 | 清理本地 Form | 否 | TODO |
| 战斗结束 | 待核验 | 待核验 | 待核验 | 待核验 | `ReleaseCombatResources` | 否 | TODO |
| 人物受击/死亡/胜利 | 待核验 | 待核验 | 待核验 | 待核验 | 原人物表现 | 否 | TODO |

## 卡牌标题显示面

| 显示面 | UI 类型/节点 | 刷新方法签名 | 原名来源 | 失败回退 | 两次确认 | 状态 |
|---|---|---|---|---|---|---|
| card_art | 待核验 | 待核验 | 待核验 | 原标题 | 否 | TODO |
| hand | 待核验 | 待核验 | 待核验 | 原标题 | 否 | TODO |
| deck_list | 待核验 | 待核验 | 待核验 | 原标题 | 否 | TODO |
| reward | 待核验 | 待核验 | 待核验 | 原标题 | 否 | TODO |
| compendium | 待核验 | 待核验 | 待核验 | 原标题 | 否 | TODO |
| tooltip | 待核验 | 待核验 | 待核验 | 原标题 | 否 | TODO |

## 卡牌 ID 清单

完整导出后按以下状态记录：

- `VERIFIED`：当前 build 中两次确认；
- `PROVISIONAL`：只在外部资料或一次扫描中出现；
- `MISSING`：名称目录中存在但当前 build 未找到；
- `NEW`：当前 build 新增、尚未设计佐助名称/卡面/动画。

| 原始 card_id | 原中/英文名 | 类型 | 生成/变化来源 | 审计状态 | 佐助显示名状态 |
|---|---|---|---|---|---|
| Strike | 待从当前 build 核验 | 普通 | - | TODO | approved（设计层） |
| Defend | 待从当前 build 核验 | 普通 | - | TODO | approved（设计层） |
| Bash | 待从当前 build 核验 | 普通 | - | TODO | approved（设计层） |
| GIANT_ROCK | 待核验真实 ID | 衍生 | 待核验 | PROVISIONAL | 千鸟锐枪（暂定） |

## 完成标准

- [ ] 记录游戏 build、程序集 SHA-256/MVID、BaseLib 和 MegaDot；
- [ ] 两次独立启动后的报告 `equivalent=true`；
- [ ] 核验战士人物、头像、选人立绘和脚底锚点；
- [ ] 核验 Strike、Defend、Bash 以及完整战士卡图目录；
- [ ] 导出当前 build 的完整战士相关 card_id；
- [ ] 核验卡牌请求、impact、状态移除和战斗结束签名；
- [ ] 核验六种标题显示面；
- [ ] 核验多人只读/本地视觉边界；
- [ ] 没有向仓库提交任何游戏或恢复资源。
