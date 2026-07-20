extends Node2D
class_name SasukeAdvancedVfxDirector

var low_flash_mode := false
var quality_scale := 1.0
var _active_effects: Array[Node] = []

func configure(low_flash: bool, quality: float = 1.0) -> void:
    low_flash_mode = low_flash
    quality_scale = clampf(quality, 0.25, 1.0)

func spawn_effect(effect_id: String, anchor: Node2D, params: Dictionary = {}) -> Node2D:
    var effect := Node2D.new()
    effect.name = "AdvancedVFX_%s" % effect_id
    effect.global_position = anchor.global_position
    add_child(effect)
    _active_effects.append(effect)

    match effect_id:
        "body_flicker_trail": _spawn_body_flicker(effect, params)
        "chidori_spin_ring": _spawn_chidori_spin_ring(effect, params)
        "whirlwind_slash_impact": _spawn_whirlwind_slash(effect, params)
        "cross_cut_finish": _spawn_cross_cut(effect, params)
        "card_afterimage_orbit": _spawn_card_afterimages(effect, params)
        "dragon_flame_launch": _spawn_dragon_flame_launch(effect, params)
        "dragon_flame_impact": _spawn_dragon_flame_impact(effect, params)
        "dragon_flame_coil": _spawn_dragon_flame_coil(effect, params)
        "shuriken_fan_launch": _spawn_shuriken_fan(effect, params)
        "shuriken_cross_impact": _spawn_shuriken_cross_impact(effect, params)
        "afterimage_split": _spawn_afterimage_split(effect, params)
        "chidori_ring_burst": _spawn_chidori_ring_burst(effect, params)
        "vulnerable_marks": _spawn_vulnerable_marks(effect, params)
        _: _spawn_dragon_flame_impact(effect, params)

    return effect

func active_effect_count() -> int:
    var count := 0
    for effect in _active_effects:
        if is_instance_valid(effect) and not effect.is_queued_for_deletion():
            count += 1
    return count

func clear_all() -> void:
    for effect in _active_effects:
        if is_instance_valid(effect):
            effect.queue_free()
    _active_effects.clear()

func _spawn_body_flicker(root: Node2D, params: Dictionary) -> void:
    var copies := clampi(int(params.get("copies", 4)), 2, 8)
    var distance := float(params.get("distance", 82.0))
    for index in range(copies):
        var streak := Line2D.new()
        var t := float(index) / maxf(1.0, copies - 1.0)
        streak.points = PackedVector2Array([Vector2(-distance * t, -62.0 + index * 5.0), Vector2(18.0, -28.0 + index * 2.0)])
        streak.width = maxf(2.0, (8.0 - index) * quality_scale)
        streak.default_color = Color(0.34, 0.60, 0.82, (0.34 - t * 0.20) if low_flash_mode else (0.62 - t * 0.34))
        streak.antialiased = true
        root.add_child(streak)
    root.modulate.a = 0.0
    var tween := root.create_tween()
    tween.tween_property(root, "modulate:a", 1.0, 0.05)
    tween.tween_property(root, "modulate:a", 0.0, 0.20 if low_flash_mode else 0.13)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_chidori_spin_ring(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 86.0))
    var branches := maxi(6, int(float(params.get("branches", 8)) * quality_scale))
    for index in range(branches):
        var line := Line2D.new()
        var start_angle := TAU * float(index) / branches
        var points := PackedVector2Array()
        for step in range(5):
            var angle := start_angle + float(step) * 0.34
            var step_radius := radius * (0.45 + 0.14 * step)
            points.append(Vector2(cos(angle), sin(angle)) * step_radius)
        line.points = points
        line.width = 2.8 * quality_scale
        line.default_color = Color(0.28, 0.82, 1.0, 0.62 if low_flash_mode else 0.92)
        line.antialiased = true
        root.add_child(line)
    root.scale = Vector2(0.45, 0.45)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.14)
    tween.parallel().tween_property(root, "rotation", 1.1, 0.22)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.30 if low_flash_mode else 0.22)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_whirlwind_slash(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 88.0))
    var width := float(params.get("width", 10.0)) * quality_scale
    var iteration := int(params.get("iteration", 0))
    var line := Line2D.new()
    var points := PackedVector2Array()
    for step in range(13):
        var angle := lerpf(-1.05, 0.86, float(step) / 12.0) + float(iteration % 4) * 0.32
        points.append(Vector2(cos(angle), sin(angle)) * radius)
    line.points = points
    line.width = width
    line.default_color = Color(0.64, 0.90, 1.0, 0.62 if low_flash_mode else 0.98)
    line.antialiased = true
    root.add_child(line)
    root.rotation = float(iteration) * 0.42
    root.scale = Vector2(0.55, 0.55)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.07)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.18 if low_flash_mode else 0.11)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_cross_cut(root: Node2D, params: Dictionary) -> void:
    var size := float(params.get("size", 118.0))
    for angle in [-0.72, 0.72]:
        var line := Line2D.new()
        line.points = PackedVector2Array([Vector2(-size * 0.55, 0).rotated(angle), Vector2(size * 0.55, 0).rotated(angle)])
        line.width = 13.0 * quality_scale
        line.default_color = Color(0.74, 0.94, 1.0, 0.62 if low_flash_mode else 1.0)
        line.antialiased = true
        root.add_child(line)
    root.scale = Vector2(0.4, 0.4)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.09)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.24 if low_flash_mode else 0.15)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_card_afterimages(root: Node2D, params: Dictionary) -> void:
    var count := clampi(int(params.get("count", 4)), 1, 12)
    var radius := float(params.get("radius", 96.0))
    for index in range(count):
        var card := Polygon2D.new()
        card.polygon = PackedVector2Array([Vector2(-9, -14), Vector2(9, -14), Vector2(9, 14), Vector2(-9, 14)])
        card.color = Color(0.72, 0.24, 0.20, 0.38 if low_flash_mode else 0.72)
        var angle := TAU * float(index) / count
        card.position = Vector2(cos(angle), sin(angle)) * radius
        card.rotation = angle + PI * 0.5
        root.add_child(card)
    root.scale = Vector2(0.45, 0.45)
    root.rotation = -0.5
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.18)
    tween.parallel().tween_property(root, "rotation", 0.55, 0.55)
    tween.tween_interval(0.18)
    tween.tween_property(root, "modulate:a", 0.0, 0.24)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_dragon_flame_launch(root: Node2D, params: Dictionary) -> void:
    var length := float(params.get("length", 260.0))
    var width := float(params.get("width", 18.0)) * quality_scale
    var flame := Line2D.new()
    var points := PackedVector2Array()
    for step in range(12):
        var t := float(step) / 11.0
        points.append(Vector2(length * t, sin(t * TAU * 1.4) * (18.0 + 12.0 * t) - 35.0 * t))
    flame.points = points
    flame.width = width
    flame.default_color = Color(1.0, 0.34, 0.12, 0.62 if low_flash_mode else 0.96)
    flame.antialiased = true
    root.add_child(flame)
    var embers := clampi(int(params.get("embers", 10)), 4, 20)
    for index in range(embers):
        var ember := Line2D.new()
        var x := length * float(index) / maxf(1.0, embers - 1.0)
        ember.points = PackedVector2Array([Vector2(x, -6.0), Vector2(x + 14.0, -28.0 - float(index % 4) * 5.0)])
        ember.width = 2.2
        ember.default_color = Color(1.0, 0.58, 0.16, 0.48 if low_flash_mode else 0.82)
        root.add_child(ember)
    root.scale.x = 0.08
    var tween := root.create_tween()
    tween.tween_property(root, "scale:x", 1.0, 0.18)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.46 if low_flash_mode else 0.34)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_dragon_flame_impact(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 42.0))
    var petals := clampi(int(params.get("petals", 7)), 4, 12)
    var iteration := int(params.get("iteration", 0))
    root.position += Vector2(float(iteration % 5) * 42.0, -float(iteration % 3) * 18.0)
    for index in range(petals):
        var flame := Line2D.new()
        var angle := TAU * float(index) / petals
        flame.points = PackedVector2Array([Vector2.ZERO, Vector2.RIGHT.rotated(angle) * radius * 0.55, Vector2.RIGHT.rotated(angle + 0.18) * radius])
        flame.width = 4.5 * quality_scale
        flame.default_color = Color(1.0, 0.40 + 0.03 * float(index % 3), 0.12, 0.50 if low_flash_mode else 0.92)
        root.add_child(flame)
    root.scale = Vector2(0.28, 0.28)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.10)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.24 if low_flash_mode else 0.15)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_dragon_flame_coil(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 132.0))
    var turns := float(params.get("turns", 2.4))
    var line := Line2D.new()
    var points := PackedVector2Array()
    var segments := maxi(18, int(42 * quality_scale))
    for index in range(segments):
        var t := float(index) / maxf(1.0, segments - 1.0)
        var angle := t * TAU * turns
        var r := radius * (0.25 + 0.75 * t)
        points.append(Vector2(cos(angle), sin(angle) * 0.62) * r)
    line.points = points
    line.width = 12.0 * quality_scale
    line.default_color = Color(1.0, 0.30, 0.10, 0.50 if low_flash_mode else 0.90)
    line.antialiased = true
    root.add_child(line)
    root.scale = Vector2(0.35, 0.35)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.20)
    tween.parallel().tween_property(root, "rotation", 0.55, 0.32)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.48 if low_flash_mode else 0.32)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_shuriken_fan(root: Node2D, params: Dictionary) -> void:
    var count := clampi(int(params.get("count", 3)), 1, 7)
    var spread := float(params.get("spread", 0.38))
    var distance := float(params.get("distance", 210.0))
    for index in range(count):
        var shuriken := Polygon2D.new()
        shuriken.polygon = PackedVector2Array([Vector2(-10, 0), Vector2(0, -3), Vector2(10, 0), Vector2(0, 3)])
        shuriken.color = Color(0.72, 0.82, 0.92, 0.58 if low_flash_mode else 0.94)
        var t := 0.5 if count == 1 else float(index) / float(count - 1)
        var angle := lerpf(-spread, spread, t)
        shuriken.rotation = angle
        shuriken.position = Vector2.RIGHT.rotated(angle) * distance * 0.25
        root.add_child(shuriken)
    root.scale = Vector2(0.35, 0.35)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.10)
    tween.parallel().tween_property(root, "position:x", distance * 0.55, 0.17)
    tween.parallel().tween_property(root, "rotation", 1.8, 0.17)
    tween.tween_property(root, "modulate:a", 0.0, 0.13)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_shuriken_cross_impact(root: Node2D, params: Dictionary) -> void:
    var size := float(params.get("size", 58.0))
    var blades := clampi(int(params.get("blades", 3)), 2, 6)
    for index in range(blades):
        var line := Line2D.new()
        var angle := TAU * float(index) / blades
        line.points = PackedVector2Array([Vector2(-size * 0.55, 0).rotated(angle), Vector2(size * 0.55, 0).rotated(angle)])
        line.width = 5.0 * quality_scale
        line.default_color = Color(0.68, 0.88, 1.0, 0.55 if low_flash_mode else 0.95)
        root.add_child(line)
    root.scale = Vector2(0.25, 0.25)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.08)
    tween.parallel().tween_property(root, "rotation", 0.75, 0.15)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.20 if low_flash_mode else 0.13)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_afterimage_split(root: Node2D, params: Dictionary) -> void:
    var copies := clampi(int(params.get("copies", 3)), 2, 6)
    var distance := float(params.get("distance", 66.0))
    for index in range(copies):
        var silhouette := Line2D.new()
        var centered := float(index) - float(copies - 1) * 0.5
        silhouette.position = Vector2(centered * distance, -28.0 + absf(centered) * 5.0)
        silhouette.points = PackedVector2Array([Vector2(0, -46), Vector2(0, 10), Vector2(-13, 42), Vector2(0, 10), Vector2(16, 40)])
        silhouette.width = 7.0 * quality_scale
        silhouette.default_color = Color(0.28, 0.48, 0.70, 0.24 if low_flash_mode else 0.46)
        root.add_child(silhouette)
    root.scale = Vector2(0.60, 0.60)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.10)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.26 if low_flash_mode else 0.17)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_chidori_ring_burst(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 150.0))
    var rings := clampi(int(params.get("rings", 3)), 1, 5)
    var branches := clampi(int(params.get("branches", 10)), 6, 20)
    for ring_index in range(rings):
        var ring := Line2D.new()
        ring.closed = true
        ring.width = maxf(2.0, (7.0 - ring_index) * quality_scale)
        ring.default_color = Color(0.28, 0.82, 1.0, (0.34 + 0.12 * ring_index) if low_flash_mode else (0.66 + 0.08 * ring_index))
        var points := PackedVector2Array()
        var segments := maxi(16, int(34 * quality_scale))
        var ring_radius := radius * (0.55 + 0.22 * ring_index)
        for segment in range(segments):
            var angle := TAU * float(segment) / segments
            var jitter := 1.0 + 0.04 * sin(float(segment * 5 + ring_index))
            points.append(Vector2(cos(angle), sin(angle) * 0.58) * ring_radius * jitter)
        ring.points = points
        root.add_child(ring)
    for branch_index in range(branches):
        var branch := Line2D.new()
        var angle := TAU * float(branch_index) / branches
        var direction := Vector2(cos(angle), sin(angle) * 0.58)
        branch.points = PackedVector2Array([direction * radius * 0.35, direction * radius * 0.68 + Vector2(0, -8 if branch_index % 2 == 0 else 8), direction * radius * 1.05])
        branch.width = 2.4 * quality_scale
        branch.default_color = Color(0.36, 0.88, 1.0, 0.52 if low_flash_mode else 0.88)
        root.add_child(branch)
    root.scale = Vector2(0.18, 0.18)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.16)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.36 if low_flash_mode else 0.25)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_vulnerable_marks(root: Node2D, params: Dictionary) -> void:
    var count := clampi(int(params.get("count", 3)), 1, 6)
    var spacing := float(params.get("spacing", 72.0))
    for index in range(count):
        var mark_root := Node2D.new()
        mark_root.position = Vector2(float(index) * spacing, -16.0 * float(index % 2))
        root.add_child(mark_root)
        for angle in [-0.72, 0.72]:
            var line := Line2D.new()
            line.points = PackedVector2Array([Vector2(-18, 0).rotated(angle), Vector2(18, 0).rotated(angle)])
            line.width = 4.0 * quality_scale
            line.default_color = Color(0.90, 0.12, 0.18, 0.42 if low_flash_mode else 0.78)
            mark_root.add_child(line)
    root.scale = Vector2(0.45, 0.45)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.12)
    tween.tween_interval(0.10)
    tween.tween_property(root, "modulate:a", 0.0, 0.22)
    tween.tween_callback(_release_effect.bind(root))

func _release_effect(effect: Node) -> void:
    _active_effects.erase(effect)
    if is_instance_valid(effect):
        effect.queue_free()