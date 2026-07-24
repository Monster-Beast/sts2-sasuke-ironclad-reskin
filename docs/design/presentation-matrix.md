# 佐助美化完整表现矩阵

## 目标规模

首个完整公开版不是“换一张皮”，而是一套完整角色演出包：

- **24 个以上人物状态/交互动作**；
- **24 个以上可复用攻击与施法 Profile**；
- **5 级战斗表现层级**；
- **当前版本战士全部卡牌卡面**，具体数量由资源审计确认；
- 普通与 Ancient 卡面输出；
- **8 个以上非战斗立绘或 CG 表面**；
- 高特效、标准、低特效三档；
- Cut-in 开关、快速战斗和低闪烁模式。

## 表现表面

| 场景 | 内容 | 目标 |
|---|---|---|
| 角色选择 | 主立绘、背景、选中动作 | 完整替换 |
| 地图/顶部栏 | 小头像、状态头像 | 完整替换 |
| 战斗入场 | 跑入/瞬身、落位、拔刀 | 独立动作 |
| 待机 | 常态、持刀、写轮眼警戒 | 至少 3 套，避免频繁切换 |
| 普通攻击 | 快斩、突刺、交叉斩 | 多 Profile 路由 |
| 重攻击 | 雷遁附刃、千鸟突进 | 强命中反馈 |
| 多段攻击 | 剑连击、手里剑、千鸟千本 | 保留段数节奏 |
| 范围攻击 | 千鸟流、豪火球、龙火 | 明确范围前缘 |
| 防御 | 剑格、钢丝、雷流偏转、蛇替身 | 与格挡量无逻辑耦合 |
| 强化 | 写轮眼开启、咒印扩散 | 回到正常待机状态 |
| 受击 | 轻受击、重受击、踉跄 | 不改变受击结算 |
| 死亡/胜利 | death_ready、death、收刀胜利 | 多人兼容 |
| 出牌侧边演出 | 角色半身 Cut-in | 可关闭，可跳过 |
| 终结技 | 麒麟、咒印二阶段组合 | 默认斩杀/显式标记触发 |
| 营火 | 休息、锻造 | 原创场景立绘或短循环 |
| 商店 | 进入、购买、拒绝/离开反馈 | 简短，不打断操作 |
| 战败 | 战败 CG | 原创、非血腥 |
| 加载 | 角色加载页与预热进度 | 降低首战卡顿 |
| 多人 | 手势、远端人物比例和状态 | 队友无需安装也不锁死 |
| 设置 | 粒子、Cut-in、闪光、日志 | 内置配置 |

## 人物动作清单

### 基础状态

1. `combat_entry_run_in`
2. `combat_entry_substitution`
3. `idle_neutral`
4. `idle_sword_ready`
5. `idle_sharingan_alert`
6. `guard_parry`
7. `guard_wire`
8. `hit_light`
9. `hit_heavy`
10. `stagger_recover`
11. `death_ready`
12. `death`
13. `victory_sheathe`
14. `buff_sharingan`
15. `debuff_curse_recoil`
16. `curse_mark_transform`
17. `campfire_rest`
18. `campfire_smith`
19. `merchant_acknowledge`
20. `merchant_decline`

### 攻击和施法

1. `sword_slash_fast`
2. `sword_thrust_fast`
3. `sword_cross_slash`
4. `sword_slash_heavy`
5. `sword_combo_three`
6. `sword_spin`
7. `projectile_burst`
8. `shuriken_wire_combo`
9. `chidori_thrust`
10. `chidori_spear`
11. `chidori_stream`
12. `chidori_stream_burst`
13. `chidori_senbon`
14. `fireball_cast`
15. `dragon_flame`
16. `fire_guard`
17. `sharingan_counter`
18. `snake_substitution`
19. `snake_guard`
20. `curse_seal_sacrifice`
21. `curse_mark_amplify`
22. `kirin_cutin`
23. `kirin_finisher`
24. `curse_mark_finisher`

## 卡面生产线

每张卡牌需要：

1. 语义分析；
2. 黑白小稿 3 版；
3. 构图评审；
4. 角色和忍术一致性评审；
5. 完整绘制；
6. 25% 缩放可读性评审；
7. 普通版导出；
8. Ancient 版导出；
9. 游戏内截图验收；
10. 资产来源与许可登记。

卡面不要求每张都出现佐助正脸。可使用手、眼、剑、火焰、雷流、蛇、钢丝和咒印纹路作为视觉主体，避免 80 多张卡变成重复人物海报。

## 演出频率原则

- Quick：高频，不打断节奏；
- Standard：大部分普通攻击；
- Signature：重击、范围、能力牌；
- Cut-in：低频，可关闭；
- Finisher：极低频，默认只在斩杀或显式终结卡触发。

## 质量门槛

- 角色不遮挡敌人意图、血量和卡牌区；
- Cut-in 不覆盖关键操作超过必要时长；
- 普通动作 60 FPS 目标下无持续帧时间尖峰；
- 重复播放 100 次后临时节点数量不增长；
- 首次播放可预热，第二次不得明显卡顿；
- 全部高亮效果都有低闪烁替代；
- 所有动作失败都能回退原表现。
