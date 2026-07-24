# 逐卡专属动画系统

## 核心原则

卡牌 ID 是动画选择的第一主键。所有攻击牌在正式发布前必须拥有唯一 `animation_id` 和独立时间轴；伤害、力量、段数、目标数与能量消耗只允许控制该卡牌动画内部的表现变体，不能改为另一套通用攻击动画。

```text
card_id
→ 唯一 animation_id
→ 基础/升级/强化/斩杀/低闪烁变体
→ 绑定原游戏命中事件
→ 资源异常时回退原动画
```

## 动画实现模式

### Bespoke

完全专属人物动作、镜头、VFX 和节奏。用于 Bash、Cleave、Thunderclap、Heavy Blade、Whirlwind、Fiend Fire 等机制鲜明的牌。

### Composed

允许复用拔刀、踏步、瞬身、结印和收刀等动作原子，但每张卡必须拥有独立编排、独立命中时间轴和独立特效组合。复用的是片段，不是完整成品动画。

### Stateful

用于能力与形态牌。播放专属演出后切换持续状态，例如 Demon Form 进入咒印二阶段待机。

## 数值参数的边界

伤害只影响该牌自己的刀光宽度、雷火密度、命中闪光、镜头强度和斩杀收尾。段数只控制该牌自己的命中事件、连击循环和目标分配。

- Heavy Blade：力量越高，雷遁附刃与纵斩压迫感越强，但始终播放 Heavy Blade 专属动作。
- Whirlwind：实际消耗能量控制专属瞬身剑舞的循环次数。
- Fiend Fire：被消耗手牌数控制卡牌残影和龙火命中节拍。
- Strike：即使伤害很高，也不会借用 Heavy Blade 或麒麟演出。

## 炫酷但不拖沓

每个动作由识别前摇、主体位移/施法、原事件同步命中、二级特效反馈和短恢复构成。普通牌尽量控制在一秒左右；指定卡牌可使用 Cut-in 或 Finisher，但必须可关闭，并提供快速与低闪烁版本。

## 首批专属动画

| 卡牌 | 动画 ID | 模式 | 演出核心 |
|---|---|---|---|
| Strike | `strike_kusanagi_draw_slash` | Composed | 写轮眼微亮、踏步拔刀、斜斩、半收刀 |
| Bash | `bash_sharingan_breaker` | Bespoke | 破绽锁定、瞬身、刀鞘破防、肘击跟进 |
| Anger | `anger_shuriken_afterimage` | Bespoke | 手里剑与残影夹击，呼应复制牌 |
| Cleave | `cleave_chidori_ground_arc` | Bespoke | 草薙剑贴地横扫，千鸟流扇形扩散 |
| Thunderclap | `thunderclap_chidori_ring_burst` | Bespoke | 双手压地、环形雷爆、破绽残光 |
| Heavy Blade | `heavy_blade_lightning_execution` | Bespoke | 雷遁蓄刃、短 Cut-in、高压纵斩 |
| Whirlwind | `whirlwind_chidori_blade_storm` | Bespoke | X 费专属瞬身剑舞与雷环连段 |
| Fiend Fire | `fiend_fire_dragon_flame_annihilation` | Bespoke | 手牌残影进入龙火并逐段引爆 |

完整数据由 `SasukeIronclad/data/card_animation_manifest.json` 管理。
