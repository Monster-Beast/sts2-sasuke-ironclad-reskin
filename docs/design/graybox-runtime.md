# 灰盒人物与逐卡时间轴运行时

## 目的

本阶段使用完全原创的几何灰盒人物验证技术链路，不把占位图误当正式美术，也不依赖动画、漫画或参考 Mod 资源。

## 运行时场景

- `sasuke_character_rig.tscn`：Hebi 佐助轮廓灰盒和稳定锚点；
- `animation_director.tscn`：逐卡 JSON 时间轴播放；
- `vfx_director.tscn`：程序化刀光、雷花、钢丝和命中环；
- `camera_effect_director.tscn`：低强度闪光、压暗和镜头震动；
- `graybox_preview.tscn`：可在 MegaDot 中独立运行的预览场景。

正式人物骨骼、贴图和粒子可以替换场景内部实现，但不得改变公开锚点和时间轴事件契约。

## 公开锚点

```text
WeaponAnchor
LeftHandAnchor
RightHandAnchor
EyeAnchor
GroundAnchor
VfxAnchor
```

卡牌时间轴只能通过这些锚点定位特效，不能直接依赖正式骨骼的内部节点名称。

## 时间轴事件

| 类型 | 用途 |
|---|---|
| `pose` | 切换人物姿态 |
| `eye` | 开关写轮眼视觉状态 |
| `vfx` | 在指定锚点生成程序化特效 |
| `camera` | 播放闪光、压暗或震动 |
| `impact` | 通知适配器原游戏命中序号 |
| `return_idle` | 回到持刀待机 |

`impact` 只是视觉同步信号，不能主动造成伤害。

## 已实现灰盒时间轴

### Strike

`strike_kusanagi_draw_slash`

写轮眼微亮、踏步拔刀、斜斩刀光、单次 impact、半收刀回待机。

### Defend

`defend_wire_parry_stance`

剑格姿态、忍者钢丝、靛蓝护弧、格挡视觉事件、回待机。

### Bash

`bash_sharingan_breaker`

写轮眼锁定、短暂压暗、瞬身式贴近、刀柄冲击、impact 和中等命中反馈。

## 变体

每个灰盒时间轴至少定义：

- `fast`：只缩短本牌时间轴；
- `low_flash`：降低写轮眼、命中闪光和镜头震动；
- 其他升级、强化和斩杀变体继续使用同一 `animation_id`。

## 独立预览

在 MegaDot 编辑器中打开并运行：

```text
SasukeIronclad/scenes/runtime/graybox_preview.tscn
```

预览器可分别播放 Strike、Defend 和 Bash，并可切换低闪烁模式。它不会执行任何伤害或卡牌逻辑。

## 接入边界

当前场景可在 MegaDot 中独立实例化，但仍未连接真实游戏 Hook。完成 Issue #1 的类型、方法、节点与命中事件审计后，再实现 `IVisualSceneHost` 的 Godot 适配器。
