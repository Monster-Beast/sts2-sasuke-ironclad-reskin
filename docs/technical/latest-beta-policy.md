# 最新 Beta 适配策略

## 目标

本项目只面向 Steam 的滚动 Beta 分支：

```text
public-beta
```

`stable`、历史 Beta build、手工固定的旧测试版本均不生成或启用游戏接口 Profile。美术资源和纯灰盒预览可以继续开发，但真实标题与视觉 Hook 只对当前远端 `public-beta` build 开放。

## 为什么不能只写一个版本号

`public-beta` 是持续更新分支。语义版本号只能作为人类可读参考，不能证明本机程序集就是 Steam 当前分发的 Beta。因此真实适配使用以下组合：

```text
Steam 远端 public-beta buildid
+ 本机 appmanifest buildid
+ 本机 BetaKey
+ sts2.dll SHA-256
+ sts2.dll Module MVID
+ BaseLib 版本
```

`latest_beta_policy.json` 中的 `latest_known_patch` 仅用于文档提示；SteamCMD 返回的远端 branch buildid 才是审计时的权威输入。

## 审计前置门

Windows 与 Linux/macOS 审计脚本会先执行：

```text
SteamCMD app_info_print 2868840
→ 提取 public-beta buildid
→ 读取本机 appmanifest_2868840.acf
→ 确认 BetaKey == public-beta
→ 确认本机 buildid == 远端 public-beta buildid
```

不满足时，脚本以非零状态退出，不生成可用于 Profile 的审阅表。

支持两种远端信息来源：

1. 由脚本直接调用 SteamCMD；
2. 通过 `-SteamCmdOutput` / `--steamcmd-output` 使用刚刚保存的 SteamCMD 输出。

第二种方式只适合网络受限环境；输出必须来自当前审计时段，不应重复使用历史文件。

## 两次扫描期间的更新保护

完整审计会在以下两个时间点重新执行最新 Beta 校验：

1. 第一次本地扫描之前；
2. 启动并退出游戏后、第二次扫描之前。

如果 Steam 在两次扫描之间发布新 Beta：

- 本机尚未更新时，第二次远端校验失败；
- 本机已经更新时，两次程序集、符号或资源比较失败；
- 两种情况都不会生成可审核 Profile。

## Beta Attestation

校验成功后生成元数据文件：

```text
local-audit/latest-beta/attestation.json
```

内容包括：

```json
{
  "required_branch": "public-beta",
  "installed_branch": "public-beta",
  "installed_build_id": "...",
  "remote_build_id": "...",
  "is_latest": true,
  "checked_at_utc": "...",
  "source": "steamcmd_app_info_print",
  "steamcmd_output_sha256": "..."
}
```

该文件不包含 Steam 账号、密码、安装绝对路径或游戏资源。

## 审阅与 Profile 约束

`build_audit_review.py` 只有在以下条件全部满足时才生成候选表：

- 两次审计 `equivalent=true`；
- 审计报告分支为 `public-beta`；
- 报告 buildid 等于 Beta Attestation 的远端 buildid；
- Attestation 状态为 `verified`；
- 本机分支和 buildid 与远端一致。

`create_integration_profile.py` 与 `compile_reviewed_profile.py` 会把 Attestation 写入 Profile，但仍强制：

```json
"status": "pending_review"
```

工具不存在自动生成 `verified` Profile 的路径。

## 运行时失效规则

即使 Profile 后续经过人工回归并晋级为 `verified`，运行时仍要求：

- 当前分支是 `public-beta`；
- 当前 Steam buildid 等于 Profile buildid；
- 程序集 SHA-256 与 MVID 完全一致；
- BaseLib 版本完全一致；
- Profile 中的远端 Beta buildid 与当前 buildid 完全一致；
- Beta Attestation 未超过配置的有效期；
- 所有目标仍是已验证的 MethodDef；
- 实际适配器已显式注册。

任一条件不满足时：

```text
不安装 Hook
→ 保留原游戏动画
→ 保留原游戏标题
```

当前有效期为 72 小时。开发期使用短有效期是为了尽快发现 Beta 更新；发布前可以在不放宽“远端证明 + 精确指纹”的前提下重新评估有效期。

## 更新处理流程

Steam 发布新 `public-beta` 后：

1. 旧 Profile 因 buildid/程序集指纹不匹配而失效；
2. 更新本机游戏和 BaseLib；
3. 重新执行两次审计；
4. 重新生成候选审阅表；
5. 对变化的 MethodDef、资源路径和卡牌 ID 重新审核；
6. 重新执行单人、多人、回退和多 Mod 回归；
7. 只保留当前最新 Beta 的有效 Profile。

禁止把旧 Beta Profile 改成宽松范围匹配，也禁止跳过 MVID、SHA-256 或远端 buildid 检查。
