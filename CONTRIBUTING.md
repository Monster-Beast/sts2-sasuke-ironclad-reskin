# Contributing

1. 只提交从零创作、已获授权或明确允许再分发的内容。
2. 不提交游戏拆包文件、动画截图、漫画扫描、官方音频、字体或 Logo。
3. 不改变卡牌数值、结算、行动队列、随机数或多人模型哈希。
4. 每个动作必须包含 `impact` 与 `complete` 事件，并允许回退原动画。
5. 每个资产必须在 `art/asset-manifest.csv` 登记作者和许可。

提交前运行：

```bash
python tools/validate_repo.py
```
