# 最新 `public-beta` 本地资源与符号审计

> 本文只记录路径、尺寸、格式、哈希、元数据 Token 和方法签名。禁止提交 PCK、DLL、贴图、动画、音频、字体、恢复工程或反编译源码。

## 审计状态

- 审计工具链：`READY`
- 目标分支：`public-beta`
- Steam 远端 Beta buildid：待填写
- 本机 Steam buildid：待填写
- Beta Attestation 时间：待填写
- `sts2.dll` SHA-256：待填写
- `sts2.dll` MVID：待填写
- BaseLib 版本：待填写
- BaseLib 清单 SHA-256：待填写
- MegaDot：4.5.1（本机实际版本待填写）
- 操作系统与架构：待填写
- 第一次审计时间：待填写
- 第二次审计时间：待填写
- 两次结果一致：待填写
- 集成 Contract：`pending_local_audit`
- 已验证 Profile：`0`
- 正式游戏 Binding：`DISABLED`

## 前置条件

1. 在 Steam 的 Betas 页面选择 `public-beta`；
2. 等待 Steam 完成更新；
3. 准备 .NET 9 和 Python 3；
4. 准备 Valve SteamCMD，或刚刚生成的 `app_info_print 2868840` 输出；
5. 可选：准备仅供本机研究的恢复资源目录。

语义版本号不能替代 buildid。审计脚本会把 SteamCMD 返回的远端 `public-beta` buildid 与本机 `appmanifest_2868840.acf` 比较，不一致时直接停止。

## 自动审计工具

仓库提供 `tools/game_audit`。工具使用 `System.Reflection.Metadata` / `PEReader` 读取托管元数据，不加载或执行 `sts2.dll`；资源扫描只输出相对路径、文件大小、图片尺寸和 SHA-256，不复制源文件。

### Windows 两次审计

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

`-AssetRoot` 可省略；省略后只能扫描游戏安装树中可见文件，无法获得 PCK 内部资源路径。

### Linux / macOS

```bash
chmod +x tools/run-game-audit.sh

./tools/run-game-audit.sh \
  --game-path "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2" \
  --asset-root "$HOME/STS2-recovered" \
  --branch public-beta \
  --steamcmd /path/to/steamcmd
```

### 手工单次扫描

单次扫描本身不能证明“最新 Beta”，只适合排错：

```powershell
dotnet run --project tools/game_audit/SasukeIronclad.GameAudit.csproj -- scan `
  --game-path "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  --asset-root "D:\STS2-recovered" `
  --output "local-audit\run-1" `
  --branch public-beta `
  --session run-1 `
  --megadot-version 4.5.1
```

正式审阅必须额外提供 `local-audit/latest-beta/attestation.json`。

## 工具输出

```text
local-audit/
├─ latest-beta/
│  ├─ steamcmd-app-info.txt
│  └─ attestation.json
├─ run-1/
│  ├─ audit-report.json
│  ├─ audit-report.md
│  └─ audit-fingerprint.json
├─ run-2/
│  ├─ audit-report.json
│  ├─ audit-report.md
│  └─ audit-fingerprint.json
├─ comparison/
│  ├─ audit-comparison.json
│  └─ audit-comparison.md
└─ review/
   ├─ binding-review.json
   └─ binding-review.md
```

只有以下条件同时满足，候选才能进入人工复核：

```text
attestation.is_latest == true
attestation.required_branch == public-beta
report.branch == public-beta
report.buildid == attestation.remote_build_id
audit-comparison.equivalent == true
```

仓库已经忽略 `local-audit/` 和 `audit-output/`。这些目录不得提交；确认后的少量结果手工整理到本文。

## 自动候选不等于已验证 Hook

一个 Hook 只有同时满足以下条件才可标记为 `VERIFIED`：

1. 记录完整类型名、方法名、参数类型、返回类型和 Metadata Token；
2. Token 是 `0x06` MethodDef；
3. `sts2.dll` SHA-256、MVID、Steam buildid 与报告一致；
4. SteamCMD 远端 `public-beta` buildid 与本机一致；
5. 两次独立游戏启动后的审计结果一致；
6. 已确认该点只读取或触发视觉，不修改伤害、费用、随机数或行动队列；
7. 失败时能够保留原游戏表现；
8. 多人模式下确认只创建本地视觉节点；
9. Beta Attestation 仍在有效期内；
10. 仓库中不存在 buildid 更高的 Profile。

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

- `VERIFIED`：当前远端最新 Beta 中两次确认；
- `PROVISIONAL`：只在外部资料或一次扫描中出现；
- `MISSING`：名称目录中存在但当前 Beta 未找到；
- `NEW`：当前 Beta 新增、尚未设计佐助名称/卡面/动画。

| 原始 `card_id` | 原中/英文名 | 类型 | 生成/变化来源 | 审计状态 | 佐助显示名状态 |
|---|---|---|---|---|---|
| Strike | 待从当前 Beta 核验 | 普通 | - | TODO | approved（设计层） |
| Defend | 待从当前 Beta 核验 | 普通 | - | TODO | approved（设计层） |
| Bash | 待从当前 Beta 核验 | 普通 | - | TODO | approved（设计层） |
| GIANT_ROCK | 待核验真实 ID | 衍生 | 待核验 | PROVISIONAL | 千鸟锐枪（暂定） |

## 完成标准

- [ ] 本机与 SteamCMD 远端 `public-beta` buildid 一致；
- [ ] 记录程序集 SHA-256/MVID、BaseLib 和 MegaDot；
- [ ] 两次独立启动后的报告 `equivalent=true`；
- [ ] 核验战士人物、头像、选人立绘和脚底锚点；
- [ ] 核验 Strike、Defend、Bash 以及完整战士卡图目录；
- [ ] 导出当前最新 Beta 的完整战士相关 `card_id`；
- [ ] 核验卡牌请求、impact、状态移除和战斗结束签名；
- [ ] 核验六种标题显示面；
- [ ] 核验多人只读/本地视觉边界；
- [ ] 没有向仓库提交任何游戏或恢复资源。
