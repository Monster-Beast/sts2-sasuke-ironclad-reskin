extends SasukeAdvancedVfxDirector
class_name SasukeStatefulAdvancedVfxDirector

const STATEFUL_EFFECTS := {
    "fire_seal_sparks": true,
    "fire_half_ring": true,
    "curse_mark_spread": true,
    "pact_card_afterimages": true,
    "pact_consumption_burst": true,
    "chakra_return": true,
    "sharingan_overdrive": true,
    "power_afterimages": true
}

func spawn_effect(effect_id: String, anchor: Node2D, params: Dictionary = {}) -> Node2D:
    if not STATEFUL_EFFECTS.has(effect_id):
        return super.spawn_effect(effect_id, anchor, params)

    var effect := Node2D.new()
    effect.name = "AdvancedVFX_%s" % effect_id
    effect.global_position = anchor.global_position
    add_child(effect)
    _active_effects.append(effect)
    match effect_id:
        "fire_seal_sparks": _spawn_fire_seal_sparks(effect, params)
        "fire_half_ring": _spawn_fire_half_ring(effect, params)
        "curse_mark_spread": _spawn_curse_mark_spread(effect, params)
        "pact_card_afterimages": _spawn_pact_card_afterimages(effect, params)
        "pact_consumption_burst": _spawn_pact_consumption_burst(effect, params)
        "chakra_return": _spawn_chakra_return(effect, params)
        "sharingan_overdrive": _spawn_sharingan_overdrive(effect, params)
        "power_afterimages": _spawn_power_afterimages(effect, params)
    return effect

func _spawn_fire_seal_sparks(root: Node2D, params: Dictionary) -> void:
    var count := clampi(int(params.get("count", 9)), 4, 18)
    for index in range(count):
        var spark := Line2D.new()
        var angle := TAU * float(index) / count
        spark.points = PackedVector2Array([Vector2.ZERO, Vector2.RIGHT.rotated(angle) * (18.0 + float(index % 3) * 7.0)])
        spark.width = 2.4 * quality_scale
        spark.default_color = Color(1.0, 0.48, 0.10, 0.44 if low_flash_mode else 0.86)
        root.add_child(spark)
    root.scale = Vector2(0.35, 0.35)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.12)
    tween.parallel().tween_property(root, "rotation", 0.75, 0.18)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.28 if low_flash_mode else 0.18)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_fire_half_ring(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 108.0))
    var layers := clampi(int(params.get("layers", 3)), 1, 4)
    for layer in range(layers):
        var line := Line2D.new()
        line.width = maxf(3.0, (12.0 - layer * 2.0) * quality_scale)
        line.default_color = Color(1.0, 0.26 + 0.08 * layer, 0.06, 0.44 if low_flash_mode else 0.86)
        line.antialiased = true
        var points := PackedVector2Array()
        var segments := maxi(14, int(30 * quality_scale))
        for index in range(segments + 1):
            var angle := lerpf(-2.95, -0.20, float(index) / segments)
            points.append(Vector2(cos(angle), sin(angle)) * (radius + layer * 13.0))
        line.points = points
        root.add_child(line)
    root.scale = Vector2(0.25, 0.25)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.16)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.38 if low_flash_mode else 0.26)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_curse_mark_spread(root: Node2D, params: Dictionary) -> void:
    var marks := clampi(int(params.get("marks", 9)), 5, 15)
    var radius := float(params.get("radius", 88.0))
    for index in range(marks):
        var line := Line2D.new()
        var angle := TAU * float(index) / marks
        line.points = PackedVector2Array([
            Vector2(cos(angle), sin(angle) * 0.62) * radius * 0.28,
            Vector2(cos(angle + 0.18), sin(angle + 0.18) * 0.62) * radius * 0.66,
            Vector2(cos(angle - 0.08), sin(angle - 0.08) * 0.62) * radius
        ])
        line.width = 3.6 * quality_scale
        line.default_color = Color(0.52, 0.18, 0.70, 0.36 if low_flash_mode else 0.78)
        root.add_child(line)
    root.scale = Vector2(0.30, 0.30)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.18)
    tween.parallel().tween_property(root, "rotation", 0.45, 0.30)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.38 if low_flash_mode else 0.26)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_pact_card_afterimages(root: Node2D, params: Dictionary) -> void:
    var count := clampi(int(params.get("count", 1)), 1, 4)
    var radius := float(params.get("radius", 98.0))
    for index in range(count):
        var card := Polygon2D.new()
        card.polygon = PackedVector2Array([Vector2(-11, -16), Vector2(11, -16), Vector2(11, 16), Vector2(-11, 16)])
        card.color = Color(0.42, 0.18, 0.58, 0.38 if low_flash_mode else 0.76)
        var angle := TAU * float(index) / count - PI * 0.5
        card.position = Vector2(cos(angle), sin(angle) * 0.66) * radius
        card.rotation = angle + PI * 0.5
        root.add_child(card)
    root.scale = Vector2(0.4, 0.4)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.16)
    tween.parallel().tween_property(root, "rotation", 0.65, 0.42)
    tween.tween_property(root, "modulate:a", 0.0, 0.28)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_pact_consumption_burst(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 94.0))
    var spokes := clampi(int(params.get("spokes", 10)), 6, 18)
    for index in range(spokes):
        var line := Line2D.new()
        var angle := TAU * float(index) / spokes
        line.points = PackedVector2Array([Vector2.RIGHT.rotated(angle) * radius, Vector2.RIGHT.rotated(angle + 0.14) * radius * 0.35, Vector2.ZERO])
        line.width = 4.0 * quality_scale
        line.default_color = Color(0.62, 0.20, 0.76, 0.40 if low_flash_mode else 0.86)
        root.add_child(line)
    root.scale = Vector2(1.25, 1.25)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2(0.15, 0.15), 0.18)
    tween.parallel().tween_property(root, "rotation", 0.55, 0.18)
    tween.tween_property(root, "modulate:a", 0.0, 0.14)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_chakra_return(root: Node2D, params: Dictionary) -> void:
    var streams := clampi(int(params.get("streams", 7)), 4, 12)
    var radius := float(params.get("radius", 76.0))
    for index in range(streams):
        var line := Line2D.new()
        var angle := TAU * float(index) / streams
        line.points = PackedVector2Array([
            Vector2.RIGHT.rotated(angle) * radius,
            Vector2.RIGHT.rotated(angle + 0.30) * radius * 0.55,
            Vector2.ZERO
        ])
        line.width = 3.0 * quality_scale
        line.default_color = Color(0.46, 0.70, 0.94, 0.34 if low_flash_mode else 0.72)
        root.add_child(line)
    var tween := root.create_tween()
    tween.tween_property(root, "rotation", 0.80, 0.22)
    tween.parallel().tween_property(root, "scale", Vector2(0.2, 0.2), 0.22)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.26)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_sharingan_overdrive(root: Node2D, params: Dictionary) -> void:
    var rings := clampi(int(params.get("rings", 3)), 1, 4)
    var radius := float(params.get("radius", 72.0))
    for ring_index in range(rings):
        var ring := Line2D.new()
        ring.closed = true
        ring.width = maxf(2.0, (7.0 - ring_index) * quality_scale)
        ring.default_color = Color(0.92, 0.08, 0.16, 0.34 if low_flash_mode else 0.82)
        var points := PackedVector2Array()
        var segments := maxi(14, int(28 * quality_scale))
        for index in range(segments):
            var angle := TAU * float(index) / segments
            points.append(Vector2(cos(angle), sin(angle)) * radius * (0.52 + ring_index * 0.22))
        ring.points = points
        root.add_child(ring)
    root.scale = Vector2(0.2, 0.2)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.14)
    tween.parallel().tween_property(root, "rotation", 1.20, 0.28)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.34 if low_flash_mode else 0.23)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_power_afterimages(root: Node2D, params: Dictionary) -> void:
    var copies := clampi(int(params.get("copies", 4)), 3, 8)
    var distance := float(params.get("distance", 76.0))
    for index in range(copies):
        var line := Line2D.new()
        var centered := float(index) - float(copies - 1) * 0.5
        line.position = Vector2(centered * distance * 0.45, -absf(centered) * 4.0)
        line.points = PackedVector2Array([Vector2(0, -58), Vector2(0, 18), Vector2(-18, 52), Vector2(0, 18), Vector2(20, 50)])
        line.width = 8.0 * quality_scale
        line.default_color = Color(0.56, 0.22, 0.76, 0.20 if low_flash_mode else 0.46)
        root.add_child(line)
    root.scale = Vector2(0.62, 0.62)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.12)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.30 if low_flash_mode else 0.20)
    tween.tween_callback(_release_effect.bind(root))
