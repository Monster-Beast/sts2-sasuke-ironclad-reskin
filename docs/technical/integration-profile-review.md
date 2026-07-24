# 集成 Profile 审核

真实游戏适配只允许绑定到一个精确安装版本。安全门要求三层状态全部通过：

```text
contract.status = verified
profile.status = verified
每个必需 binding.status = verified
```

同时必须完全匹配：

```text
branch + Steam buildid + sts2.dll SHA-256 + Module MVID + BaseLib version
```

## 生成待审核模板

完成两次本地审计并得到 `equivalent=true` 后执行：

```powershell
python tools/create_integration_profile.py `
  --report "local-audit\run-1\audit-report.json" `
  --comparison "local-audit\comparison\audit-comparison.json" `
  --output "local-audit\integration-profile-template.json"
```

生成器只填写版本指纹，并为所有必需视觉事件和标题显示面建立空白项。所有状态保持：

```json
"status": "pending_review"
```

它不会修改正式契约，也不会自动选择候选类型或方法。

## 人工复核要求

每个绑定至少记录并核对：

- 完整类型名；
- 返回类型、方法名和全部参数类型；
- Metadata Token；
- 两次审计中相同的签名与 Token；
- 对应的程序集 SHA-256、MVID 和 Steam buildid；
- 失败时使用原游戏表现的回退策略；
- 多人模式中仅创建本地视觉节点。

方法名称相似不能作为验证依据。必须在当前游戏中确认实际触发时机，并确认不会修改卡牌、伤害、费用、随机数或战斗推进。

## 状态提升顺序

```text
binding: pending_review → verified
profile: pending_review → verified
contract: pending_local_audit → verified
```

只有所有必需视觉事件和六种标题显示面都完成验证后，才可以提升 Profile 和 Contract。

## 游戏更新

以下任一值变化都会使旧 Profile 不匹配：

- Steam 分支或 buildid；
- `sts2.dll` SHA-256；
- Module MVID；
- BaseLib 版本。

更新后必须重新执行两次审计，不得复制旧版本的 Token 或签名。

## 提交边界

不得提交 `local-audit/`、游戏程序集、PCK、恢复资源、原始贴图、音频或包含本机绝对路径的日志。只提交经过确认的少量元数据和适配代码。
