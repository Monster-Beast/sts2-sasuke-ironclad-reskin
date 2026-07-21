# 审计候选绑定复核流程

## 目标

把两次一致的本地游戏审计结果转换为**可人工复核、但仍默认禁用**的接口 Profile。整个流程不会自动选择接口，也不会自动把状态改成 `verified`。

```text
两次本地扫描
→ equivalent=true
→ 生成候选审阅表
→ 人工观察真实游戏调用
→ 填写候选、证据和审阅人
→ 编译 pending_review Profile
→ 游戏内回归
→ 最后才允许人工改成 verified
```

## 1. 完成两次扫描

Windows：

```powershell
tools\run-game-audit.ps1 `
  -GamePath "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  -AssetRoot "D:\STS2-recovered" `
  -Branch stable
```

Linux/macOS：

```bash
./tools/run-game-audit.sh \
  --game-path "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2" \
  --asset-root "$HOME/sts2-recovered" \
  --branch stable
```

必须得到：

```text
local-audit/run-1/audit-report.json
local-audit/run-2/audit-report.json
local-audit/comparison/audit-comparison.json
local-audit/review/binding-review.json
local-audit/review/binding-review.md
```

并确认：

```json
"equivalent": true
```

Windows 与 Linux/macOS 辅助脚本会在比较成功后自动尝试生成审阅表。没有 Python 时可稍后手工运行 `tools/build_audit_review.py`。

## 2. 手工生成接口候选审阅表

```bash
python tools/build_audit_review.py \
  --report local-audit/run-1/audit-report.json \
  --comparison local-audit/comparison/audit-comparison.json \
  --output local-audit/review
```

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

## 3. 审阅卡牌 ID 候选

审计工具会额外记录字段、属性、事件和安全的元数据常量。`binding-review.json` 中的 `card_id_candidates` 用于核验：

- 普通 Ironclad 卡；
- 多人专属卡；
- 衍生/生成卡；
- 变化目标卡；
- 临时与特殊卡。

元数据常量只是候选。不能仅凭字段名称把它写入正式卡牌映射；必须在图鉴、奖励、生成或实际出牌中观察到同一 ID。

## 4. 人工填写每个 Binding

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
7. 被选目标是 MethodDef，并与报告中的声明类型、签名和 Token 完全一致。

## 5. 编译已审阅 Profile

```bash
python tools/compile_reviewed_profile.py \
  --review local-audit/review/binding-review.json \
  --output local-audit/review/profile.pending.json
```

即使所有候选都通过人工审阅，输出仍强制为：

```json
"status": "pending_review"
```

所有 visual/title binding 也仍是 `pending_review`。编译工具没有生成 `verified` 的代码路径。

## 6. 真实游戏回归后再晋级

晋级前必须完成：

- 主 Mod 使用当前游戏程序集完整构建；
- 单人战斗从进入到结束；
- 资源缺失回退；
- 标题适配失败回退；
- 多段攻击 index 校验；
- 状态/Form 移除；
- 多人本地视觉边界；
- 与其他 Mod 共存；
- Stable/Beta 分开测试；
- 第二名审阅者复核。

只有完成这些回归，才允许人工将 Contract、Profile 和 Binding 三层状态改成 `verified`。

## 启动保护

`MainFile` 不再调用全局 `Harmony.PatchAll()`。启动时会：

1. 收集当前进程中 `sts2.dll` 的 SHA-256 和 MVID；
2. 读取 Steam buildid；
3. 优先读取显式分支或 `STS2_BRANCH`，否则从 Steam `BetaKey` 推断，公开分支按 `stable` 处理；
4. 从游戏本地 Mods 目录和 `steamapps/workshop/content/2868840` 中定位 BaseLib 清单；
5. 通过 `GameIntegrationGate` 精确匹配 Profile；
6. 使用默认拒绝安装的适配器；
7. 在没有实际审阅适配器时保持所有接口禁用。

分支名只是指纹的一部分。即使分支推断成功，Steam buildid、程序集 SHA-256、MVID 和 BaseLib 版本仍必须全部一致。

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

不要提交真实 DLL、PCK、贴图、音频、恢复工程、绝对路径日志或未经回归便标记为 `verified` 的 Profile。
