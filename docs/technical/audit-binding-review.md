# 审计候选绑定复核流程

## 目标

把两次一致的**滚动最新 `public-beta`** 本地审计结果转换为可人工复核、但仍默认禁用的接口 Profile。整个流程不会自动选择接口，也不会自动把状态改成 `verified`。

```text
远端 public-beta 校验
→ 两次本地扫描
→ equivalent=true
→ 生成候选审阅表
→ 人工观察真实游戏调用
→ 填写候选、证据和审阅人
→ 编译 pending_review Profile
→ 游戏内回归
→ 最后才允许人工改成 verified
```

## 1. 确认 Steam 当前 Beta

项目只接受：

```text
BetaKey = public-beta
本机 buildid = SteamCMD 返回的 public-beta buildid
```

Windows：

```powershell
tools\run-game-audit.ps1 `
  -GamePath "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  -AssetRoot "D:\STS2-recovered" `
  -Branch public-beta `
  -SteamCmdPath "C:\steamcmd\steamcmd.exe"
```

Linux/macOS：

```bash
./tools/run-game-audit.sh \
  --game-path "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2" \
  --asset-root "$HOME/sts2-recovered" \
  --branch public-beta \
  --steamcmd /path/to/steamcmd
```

网络受限时，可以通过 `-SteamCmdOutput` 或 `--steamcmd-output` 使用刚刚保存的 `app_info_print 2868840` 输出。历史输出不能作为新审计的证明。

脚本会在 run-1 前和 run-2 前各执行一次远端校验；如果两次扫描期间 Steam 发布新 Beta，流程会停止，而不是把两个不同 Build 的结果混成一个 Profile。

## 2. 必需输出

```text
local-audit/latest-beta/attestation.json
local-audit/run-1/audit-report.json
local-audit/run-2/audit-report.json
local-audit/comparison/audit-comparison.json
local-audit/review/binding-review.json
local-audit/review/binding-review.md
```

必须同时确认：

```json
{
  "equivalent": true,
  "latest_beta": {
    "is_latest": true,
    "required_branch": "public-beta"
  }
}
```

Windows 与 Linux/macOS 辅助脚本会在比较成功后自动生成审阅表。Python 3 是最新 Beta 校验和审阅表生成的必要依赖。

## 3. 手工生成接口候选审阅表

```bash
python tools/build_audit_review.py \
  --report local-audit/run-1/audit-report.json \
  --comparison local-audit/comparison/audit-comparison.json \
  --latest-beta-attestation local-audit/latest-beta/attestation.json \
  --output local-audit/review
```

生成器会拒绝：

- Stable 报告；
- 历史 Beta 报告；
- 报告 buildid 与远端 Attestation 不一致；
- `is_latest=false`；
- 两次审计不一致；
- 无效或缺失的 SteamCMD 输出摘要。

审阅表覆盖六类视觉事件：

```text
card_visual_request
original_impact
state_removed
form_removed
combat_ended
character_state
```

以及六种标题显示面：

```text
card_art
hand
deck_list
reward
compendium
tooltip
```

工具只做候选排序。它不会填写 `selected_candidate_id`，也不会把状态从 `unreviewed` 改成 `approved`。

### Binding 只能选择 MethodDef

字段、属性和事件会保留在审计报告中，用于理解卡牌 ID、UI 结构和生命周期，但不能直接成为正式 Hook 目标。正式 Binding 只能选择：

```text
kind == method
metadata_token 以 0x06 开头
```

属性必须定位到对应 getter、setter 或刷新方法；事件必须定位到明确的触发、订阅或处理方法。`compile_reviewed_profile.py` 和运行时 `GameIntegrationGate` 都会拒绝 PropertyDef、EventDef、FieldDef 等非方法 Token。

## 4. 审阅卡牌 ID 候选

审计工具会额外记录字段、属性、事件和安全的元数据常量。`binding-review.json` 中的 `card_id_candidates` 用于核验：

- 普通 Ironclad 卡；
- 多人专属卡；
- 衍生/生成卡；
- 变化目标卡；
- 临时与特殊卡。

元数据常量只是候选。不能仅凭字段名称把它写入正式卡牌映射；必须在图鉴、奖励、生成或实际出牌中观察到同一 ID。

每次 Beta 更新后都要重新导出完整清单，不能假设旧 Beta 的卡牌 ID 集合没有变化。

## 5. 人工填写每个 Binding

每个 Binding 的 `review` 必须填写：

```json
{
  "status": "approved",
  "selected_candidate_id": "审阅表中的 candidate_id",
  "reviewer": "审阅人",
  "evidence": [
    "observed_run_1",
    "observed_run_2",
    "fallback_verified",
    "local_visual_only_verified"
  ],
  "notes": "观察方式、参数语义和多人结论"
}
```

最低证据要求：

1. 两次独立启动均观察到同一入口；
2. 资源或 Mod 逻辑失败时原游戏表现仍然存在；
3. 多人模式只创建本地视觉节点；
4. 不写入伤害、费用、目标、随机数或行动队列；
5. `original_impact` 的 index 与原游戏真实段数一致；
6. 标题入口只改显示文本，不改内部 `card_id` 和规则描述；
7. 被选目标是 MethodDef，并与报告中的声明类型、签名和 Token 完全一致；
8. 观察所用 buildid 与 Attestation 中的远端 `public-beta` buildid 一致。

## 6. 编译已审阅 Profile

```bash
python tools/compile_reviewed_profile.py \
  --review local-audit/review/binding-review.json \
  --output local-audit/review/profile.pending.json
```

即使所有候选都通过人工审阅，输出仍强制为：

```json
"status": "pending_review"
```

所有 visual/title binding 也仍是 `pending_review`。编译工具没有生成 `verified` 的代码路径。Profile 会保留 Beta Attestation 的 buildid、校验时间和 SteamCMD 输出摘要。

## 7. 真实游戏回归后再晋级

晋级前必须完成：

- 主 Mod 使用当前最新 Beta 程序集完整构建；
- 单人战斗从进入到结束；
- 资源缺失回退；
- 标题适配失败回退；
- 多段攻击 index 校验；
- 状态/Form 移除；
- 多人本地视觉边界；
- 与其他 Mod 共存；
- 第二名审阅者复核；
- Beta Attestation 仍在有效期内；
- 仓库中没有更高 buildid 的 Profile。

只有完成这些回归，才允许人工将 Contract、Profile 和 Binding 三层状态改成 `verified`。

## 8. 启动保护

`MainFile` 不再调用全局 `Harmony.PatchAll()`。启动时会：

1. 收集当前进程中 `sts2.dll` 的 SHA-256 和 MVID；
2. 读取 Steam buildid；
3. 从显式参数、`STS2_BRANCH` 或 Steam `BetaKey` 确认 `public-beta`；
4. 从本地 Mods 和 `steamapps/workshop/content/2868840` 定位 BaseLib 清单；
5. 精确匹配 Profile；
6. 检查 Beta Attestation 的 build、时间、来源和摘要；
7. 检查该 Profile 是否已被更高 buildid 取代；
8. 使用默认拒绝安装的适配器；
9. 在没有实际审阅适配器时保持所有接口禁用。

安装器真正处理 MethodDef 前，还必须通过 `AuditedMethodBindingResolver` 二次核验：

- Metadata Token 必须是 `0x06` MethodDef；
- Token 必须能在已加载的游戏 Module 中解析；
- Module MVID 必须与 Profile 一致；
- 声明类型必须完全一致；
- 完整方法签名必须完全一致；
- 不接受抽象方法或开放泛型方法。

即使有人误把 Contract 改成 `verified`，未注册真实适配器时也不会安装游戏接口。

## 禁止提交

以下目录均在 `.gitignore` 中：

```text
local-audit/
audit-output/
```

不要提交真实 DLL、PCK、贴图、音频、恢复工程、绝对路径日志、Steam 账号信息或未经回归便标记为 `verified` 的 Profile。
