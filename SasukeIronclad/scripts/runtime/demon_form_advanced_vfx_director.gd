extends SasukeStatefulAdvancedVfxDirector
class_name SasukeDemonFormAdvancedVfxDirector

func spawn_effect(effect_id: String, anchor: Node2D, params: Dictionary = {}) -> Node2D:
    if effect_id != "curse_mark_consume":
        return super.spawn_effect(effect_id, anchor, params)

    var effect := Node2D.new()
    effect.name = "AdvancedVFX_%s" % effect_id
    effect.global_position = anchor.global_position
    add_child(effect)
    _active_effects.append(effect)
    _spawn_curse_mark_consume(effect, params)
    return effect

func _spawn_curse_mark_consume(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 118.0))
    var marks := clampi(int(params.get("marks", 14)), 6, 20)
    for index in range(marks):
        var line := Line2D.new()
        var angle := TAU * float(index) / marks
        var outer := Vector2(cos(angle), sin(angle) * 0.64) * radius
        var middle := Vector2(cos(angle + 0.18), sin(angle + 0.18) * 0.64) * radius * 0.58
        line.points = PackedVector2Array([outer, middle, Vector2.ZERO])
        line.width = 4.0 * quality_scale
        line.default_color = Color(0.68, 0.18, 0.86, 0.40 if low_flash_mode else 0.90)
        root.add_child(line)
    root.scale = Vector2(1.25, 1.25)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2(0.16, 0.16), 0.20)
    tween.parallel().tween_property(root, "rotation", 0.75, 0.20)
    tween.tween_property(root, "modulate:a", 0.0, 0.16 if low_flash_mode else 0.10)
    tween.tween_callback(_release_effect.bind(root))
