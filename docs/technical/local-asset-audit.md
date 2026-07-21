# 当前安装版本本地资源与符号审计

> 本文只记录路径、尺寸、格式、哈希、元数据 Token、常量和方法签名。禁止提交 PCK、DLL、贴图、动画、音频、字体、恢复工程或反编译源码。

## 当前状态

- 审计工具链：`READY`
- 真实 Stable 审计：`TODO`
- 真实 Beta 审计：`TODO / 按需`
- 集成 Contract：`pending_local_audit`
- 已验证 Profile：`0`
- 正式游戏 Binding：`DISABLED`
- 默认适配器：拒绝安装未经审计的 Hook

## 版本指纹

| 项目 | Stable | Beta | 状态 |
|---|---|---|---|
| Steam buildid | 待填写 | 待填写 | TODO |
| `sts2.dll` SHA-256 | 待填写 | 待填写 | TODO |
| `sts2.dll` MVID | 待填写 | 待填写 | TODO |
| BaseLib 版本 | 待填写 | 待填写 | TODO |
| BaseLib 清单 SHA-256 | 待填写 | 待填写 | TODO |
| Steam BetaKey / 分支 | stable | 待填写 | TODO |
| MegaDot | 4.5.1 | 4.5.1 | 基线 |
| 操作系统与架构 | 待填写 | 待填写 | TODO |
| 第一次审计时间 | 待填写 | 待填写 | TODO |
| 第二次审计时间 | 待填写 | 待填写 | TODO |
| 两次结果 `equivalent` | 待填写 | 待填写 | TODO |

## 自动审计工具

`tools/game_audit` 使用 `System.Reflection.Metadata` / `PEReader` 读取托管元数据，不加载或执行 `sts2.dll`。它会输出：

- 类型、方法、字段、属性和事件候选；
- Metadata Token、完整签名、可见性；
- 安全的常量元数据，例如候选卡牌 ID；
- 相对资源路径、文件大小、图片尺寸和可选 SHA-256；
- Steam buildid、程序集 SHA-256/MVID 和 BaseLib 版本；
- 不包含源安装目录和恢复目录的绝对路径。

### Windows 两次审计

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-game-audit.ps1 `
  -GamePath "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  -AssetRoot "D:\STS2-recovered" `
  -Branch stable
```

### Linux / macOS 两次审计

```bash
chmod +x tools/run-game-audit.sh
./tools/run-game-audit.sh \
  --game-path "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2" \
  --asset-root "$HOME/STS2-recovered" \
  --branch stable
```

`AssetRoot` 可以省略；省略后无法获得 PCK 内部恢复资源的相对路径。

脚本执行：

```text
run-1
→ 完整启动并退出一次游戏
→ run-2
→ audit-comparison
→ binding-review.json / binding-review.md
```

## 自动输出

```text
local-audit/
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

仓库已忽略 `local-audit/` 与 `audit-output/`。只有 `audit-comparison.json` 中 `equivalent=true` 时，才允许进入人工候选审阅。

## 候选不等于已验证 Hook

候选排序不会自动选择目标。正式 Binding 只能选择：

```text
kind == method
metadata_token == 0x06xxxxxx
```

字段、属性和事件只用于理解卡牌 ID、UI 结构和生命周期。属性必须定位到 getter、setter 或刷新方法；事件必须定位到触发、订阅或处理方法。

每个 Binding 必须同时具备：

1. 完整声明类型、方法签名和 MethodDef Token；
2. 两次独立启动观察；
3. 与精确 Steam buildid、程序集 SHA-256/MVID 和 BaseLib 版本一致；
4. 资源或适配器失败时保留原游戏表现；
5. 不写入伤害、费用、目标、随机数或行动队列；
6. 多人模式仅创建本地视觉节点；
7. 安装前再次通过 Token、MVID、声明类型和签名解析。

## 人物与静态资源

| 项目 | 相对资源路径/节点类型 | 类型/尺寸 | 两次确认 | 状态 |
|---|---|---|---|---|
| 战斗人物模型/场景 | 待核验 | 待核验 | 否 | TODO |
| 人物视觉根节点 | 待核验 | NodePath/类型待核验 | 否 | TODO |
| 人物脚底锚点 | 待核验 | NodePath/类型待核验 | 否 | TODO |
| 头像 | 待核验 | 待核验 | 否 | TODO |
| 选人立绘 | 待核验 | 待核验 | 否 | TODO |
| 地图/顶部头像 | 待核验 | 待核验 | 否 | TODO |
| Strike 卡图 | 待核验 | 待核验 | 否 | TODO |
| Defend 卡图 | 待核验 | 待核验 | 否 | TODO |
| Bash 卡图 | 待核验 | 待核验 | 否 | TODO |
| 战士完整卡图目录 | 待核验 | 目录清单指纹待核验 | 否 | TODO |
| 卡牌标题字体/Label | 待核验 | Node/Theme 待核验 | 否 | TODO |

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

## 必需视觉 Binding

| Binding | 声明类型 | 完整签名 | MethodDef Token | Run 1 | Run 2 | 回退 | 多人本地化 | 状态 |
|---|---|---|---|---|---|---|---|---|
| `card_visual_request` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | 未验证 | TODO |
| `original_impact` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | 未验证 | TODO |
| `state_removed` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | 未验证 | TODO |
| `form_removed` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | 未验证 | TODO |
| `combat_ended` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | 未验证 | TODO |
| `character_state` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | 未验证 | TODO |

### `original_impact` 验收

- 单段攻击 index 从 0 开始；
- Whirlwind 和 Fiend Fire 的段数与原游戏一致；
- 高段数只允许压缩 VFX，不允许丢失 index；
- 超时、资源失败或 Mod 异常时原游戏结算继续；
- Mod 不主动造成伤害、不改变目标、不写入行动队列。

## 必需标题 Binding

| Surface | 声明类型 | 完整签名 | MethodDef Token | Run 1 | Run 2 | 原标题回退 | 状态 |
|---|---|---|---|---|---|---|---|
| `card_art` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | TODO |
| `hand` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | TODO |
| `deck_list` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | TODO |
| `reward` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | TODO |
| `compendium` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | TODO |
| `tooltip` | 待核验 | 待核验 | 待核验 | 否 | 否 | 未验证 | TODO |

标题 Hook 只能替换玩家看到的标题，不能改规则文本、费用、升级状态或内部 `card_id`。

## 卡牌 ID 清单

`binding-review.json` 的 `card_id_candidates` 需要在图鉴、奖励、生成或实际出牌中人工确认。

| 原始 card_id | 原中/英文名 | 类型 | 生成/变化来源 | 审计状态 | 佐助显示名状态 |
|---|---|---|---|---|---|
| Strike | 待从当前 build 核验 | 普通 | - | TODO | approved（设计层） |
| Defend | 待从当前 build 核验 | 普通 | - | TODO | approved（设计层） |
| Bash | 待从当前 build 核验 | 普通 | - | TODO | approved（设计层） |
| GIANT_ROCK | 待核验真实 ID | 衍生 | 待核验 | PROVISIONAL | 千鸟锐枪（暂定） |
| 其余普通卡 | 从候选清单核验 | 普通 | 待核验 | TODO | 待设计/审核 |
| 多人卡 | 从候选清单核验 | 多人 | 待核验 | TODO | 待设计/审核 |
| 临时/生成/变化卡 | 从候选清单核验 | 特殊 | 待核验 | TODO | 待设计/审核 |

## Profile 晋级清单

`compile_reviewed_profile.py` 即使收到完整人工证据，也只能输出 `pending_review`。晋级到 `verified` 前必须：

- [ ] 主 Mod 使用该精确游戏程序集构建成功；
- [ ] Contract/Profile/Binding 指纹一致；
- [ ] MethodDef 二次解析通过 MVID、Token、声明类型和签名；
- [ ] 单人完整战斗回归；
- [ ] 视觉资源缺失回退；
- [ ] 标题适配失败回退；
- [ ] 多段 impact 回归；
- [ ] 状态/Form 移除回归；
- [ ] 战斗结束释放回归；
- [ ] 多人只创建本地视觉节点；
- [ ] 与其他 Mod 共存；
- [ ] Stable/Beta 分开建立 Profile；
- [ ] 第二名审阅者复核。

## 完成标准

- [ ] 记录游戏 build、程序集 SHA-256/MVID、BaseLib 和 MegaDot；
- [ ] 两次独立启动后的报告 `equivalent=true`；
- [ ] 核验人物、头像、选人立绘和脚底锚点；
- [ ] 核验 Strike、Defend、Bash 与完整战士卡图目录；
- [ ] 导出并人工确认完整战士相关 `card_id`；
- [ ] 核验六类视觉事件和六种标题显示面；
- [ ] 核验多人只读/本地视觉边界；
- [ ] 没有向仓库提交任何游戏或恢复资源。

详细步骤见 [`audit-binding-review.md`](audit-binding-review.md)。
