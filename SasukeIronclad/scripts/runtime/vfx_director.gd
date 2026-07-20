extends Node2D
class_name SasukeVfxDirector

var low_flash_mode := false
var quality_scale := 1.0
var _active_effects: Array[Node] = []

func configure(low_flash: bool, quality: float = 1.0) -> void:
    low_flash_mode = low_flash
    quality_scale = clampf(quality, 0.25, 1.0)

func spawn_effect(effect_id: String, anchor: Node2D, params: Dictionary = {}) -> Node2D:
    var effect := Node2D.new()
    effect.name = "VFX_%s" % effect_id
    effect.global_position = anchor.global_position
    add_child(effect)
    _active_effects.append(effect)

    match effect_id:
        "silver_arc": _spawn_arc(effect, Color(0.78, 0.88, 1.0, 0.9), params)
        "silver_ground_arc": _spawn_arc(effect, Color(0.78, 0.88, 1.0, 0.92), params)
        "indigo_guard_arc": _spawn_arc(effect, Color(0.30, 0.38, 0.82, 0.7), params)
        "wire_glint": _spawn_wire(effect)
        "minor_blue_sparks": _spawn_sparks(effect, params)
        "lightning_blade_charge": _spawn_lightning_blade_charge(effect, params)
        "chidori_ground_fan": _spawn_chidori_ground_fan(effect, params)
        "multi_target_impact": _spawn_multi_target_impacts(effect, params)
        "vertical_lightning_slash": _spawn_vertical_lightning_slash(effect, params)
        "focused_lightning_pillar": _spawn_focused_lightning_pillar(effect, params)
        "red_target_line": _spawn_target_line(effect)
        "short_impact_ring": _spawn_ring(effect, params)
        _: _spawn_ring(effect, params)

    return effect

func clear_all() -> void:
    for effect in _active_effects:
        if is_instance_valid(effect):
            effect.queue_free()
    _active_effects.clear()

func _spawn_arc(root: Node2D, color: Color, params: Dictionary) -> void:
    var line := Line2D.new()
    line.width = float(params.get("width", 11.0)) * quality_scale
    line.default_color = color
    line.antialiased = true
    var radius := float(params.get("radius", 90.0))
    var points := PackedVector2Array()
    var segments := maxi(6, int(18 * quality_scale))
    for index in range(segments + 1):
        var angle := lerpf(-1.15, 0.55, float(index) / segments)
        points.append(Vector2(cos(angle), sin(angle)) * radius)
    line.points = points
    root.add_child(line)
    root.rotation = float(params.get("rotation", -0.25))
    root.scale = Vector2(0.55, 0.55)
    root.modulate.a = 0.0
    var tween := root.create_tween()
    tween.tween_property(root, "modulate:a", 1.0, 0.04)
    tween.parallel().tween_property(root, "scale", Vector2.ONE, 0.12)
    tween.tween_property(root, "modulate:a", 0.0, 0.16 if low_flash_mode else 0.10)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_wire(root: Node2D) -> void:
    var line := Line2D.new()
    line.width = 2.0
    line.default_color = Color(0.75, 0.82, 0.94, 0.8)
    line.points = PackedVector2Array(Vector2(-40, 22), Vector2(0, -8), Vector2(58, 12))
    root.add_child(line)
    root.modulate.a = 0.0
    var tween := root.create_tween()
    tween.tween_property(root, "modulate:a", 0.8, 0.06)
    tween.tween_interval(0.12)
    tween.tween_property(root, "modulate:a", 0.0, 0.14)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_sparks(root: Node2D, params: Dictionary) -> void:
    var count := maxi(3, int(float(params.get("count", 9)) * quality_scale))
    for index in range(count):
        var spark := Line2D.new()
        var angle := TAU * float(index) / count
        var length := 18.0 + float(index % 3) * 8.0
        spark.points = PackedVector2Array(Vector2.ZERO, Vector2.RIGHT.rotated(angle) * length)
        spark.width = 2.5
        spark.default_color = Color(0.3, 0.82, 1.0, 0.9)
        root.add_child(spark)
    root.scale = Vector2(0.4, 0.4)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.10)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.18 if low_flash_mode else 0.12)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_lightning_blade_charge(root: Node2D, params: Dictionary) -> void:
    var blade := Line2D.new()
    blade.points = PackedVector2Array(Vector2(-8, 44), Vector2(4, 12), Vector2(20, -24), Vector2(34, -76))
    blade.width = 5.0 * quality_scale
    blade.default_color = Color(0.52, 0.90, 1.0, 0.88 if low_flash_mode else 1.0)
    blade.antialiased = true
    root.add_child(blade)

    var count := maxi(4, int(float(params.get("count", 12)) * quality_scale))
    for index in range(count):
        var branch := Line2D.new()
        var y := lerpf(36.0, -70.0, float(index) / maxf(1.0, count - 1.0))
        var side := -1.0 if index % 2 == 0 else 1.0
        branch.points = PackedVector2Array(Vector2(8, y), Vector2(18 * side, y - 8), Vector2(30 * side, y + 3))
        branch.width = 1.8
        branch.default_color = Color(0.30, 0.80, 1.0, 0.75)
        root.add_child(branch)

    root.modulate.a = 0.0
    var tween := root.create_tween()
    tween.tween_property(root, "modulate:a", 1.0, 0.08)
    tween.tween_interval(0.18)
    tween.tween_property(root, "modulate:a", 0.0, 0.20 if low_flash_mode else 0.13)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_chidori_ground_fan(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 180.0))
    var branches := maxi(5, int(float(params.get("branches", 9)) * quality_scale))
    for index in range(branches):
        var t := float(index) / maxf(1.0, branches - 1.0)
        var angle := lerpf(-0.62, 0.62, t)
        var direction := Vector2.RIGHT.rotated(angle)
        var line := Line2D.new()
        line.width = 3.0 * quality_scale
        line.default_color = Color(0.28, 0.82, 1.0, 0.68 if low_flash_mode else 0.94)
        line.antialiased = true
        line.points = PackedVector2Array(
            Vector2.ZERO,
            direction * radius * 0.34 + Vector2(0, -10 if index % 2 == 0 else 8),
            direction * radius * 0.68 + Vector2(0, 7 if index % 3 == 0 else -6),
            direction * radius
        )
        root.add_child(line)
    root.scale = Vector2(0.35, 0.35)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE, 0.16)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.30 if low_flash_mode else 0.22)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_multi_target_impacts(root: Node2D, params: Dictionary) -> void:
    var count := maxi(1, int(params.get("count", 3)))
    var spacing := float(params.get("spacing", 70.0))
    for index in range(count):
        var ring_root := Node2D.new()
        ring_root.position = Vector2(float(index) * spacing, -12.0 * float(index % 2))
        root.add_child(ring_root)
        var ring := Line2D.new()
        ring.closed = true
        ring.width = 5.0 * quality_scale
        ring.default_color = Color(0.50, 0.88, 1.0, 0.55 if low_flash_mode else 0.88)
        var points := PackedVector2Array()
        var segments := maxi(8, int(18 * quality_scale))
        for segment in range(segments):
            var angle := TAU * float(segment) / segments
            points.append(Vector2(cos(angle), sin(angle)) * 24.0)
        ring.points = points
        ring_root.add_child(ring)
    root.scale = Vector2(0.35, 0.35)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2(1.15, 1.15), 0.17)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.22)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_vertical_lightning_slash(root: Node2D, params: Dictionary) -> void:
    var height := float(params.get("height", 240.0))
    var width := float(params.get("width", 20.0)) * quality_scale
    var slash := Line2D.new()
    slash.points = PackedVector2Array(Vector2(0, -height * 0.55), Vector2(8, -height * 0.12), Vector2(-4, height * 0.25), Vector2(2, height * 0.55))
    slash.width = width
    slash.default_color = Color(0.78, 0.94, 1.0, 0.70 if low_flash_mode else 1.0)
    slash.antialiased = true
    root.add_child(slash)
    root.scale.y = 0.12
    root.modulate.a = 0.0
    var tween := root.create_tween()
    tween.tween_property(root, "modulate:a", 1.0, 0.04)
    tween.parallel().tween_property(root, "scale:y", 1.0, 0.10)
    tween.tween_property(root, "modulate:a", 0.0, 0.24 if low_flash_mode else 0.14)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_focused_lightning_pillar(root: Node2D, params: Dictionary) -> void:
    var height := float(params.get("height", 280.0))
    var branches := maxi(4, int(float(params.get("branches", 7)) * quality_scale))
    var core := Line2D.new()
    core.points = PackedVector2Array(Vector2(0, 34), Vector2(-8, -height * 0.25), Vector2(7, -height * 0.58), Vector2(0, -height))
    core.width = 8.0 * quality_scale
    core.default_color = Color(0.72, 0.94, 1.0, 0.62 if low_flash_mode else 0.95)
    core.antialiased = true
    root.add_child(core)
    for index in range(branches):
        var branch := Line2D.new()
        var y := -height * (0.18 + 0.72 * float(index) / branches)
        var side := -1.0 if index % 2 == 0 else 1.0
        branch.points = PackedVector2Array(Vector2(0, y), Vector2(34 * side, y - 18), Vector2(52 * side, y + 2))
        branch.width = 2.2
        branch.default_color = Color(0.28, 0.82, 1.0, 0.76)
        root.add_child(branch)
    root.scale.y = 0.18
    var tween := root.create_tween()
    tween.tween_property(root, "scale:y", 1.0, 0.09)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.34 if low_flash_mode else 0.24)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_target_line(root: Node2D) -> void:
    var line := Line2D.new()
    line.points = PackedVector2Array(Vector2(-50, 0), Vector2(72, 0))
    line.width = 3.0
    line.default_color = Color(0.9, 0.12, 0.18, 0.55 if low_flash_mode else 0.9)
    root.add_child(line)
    root.scale.x = 0.1
    var tween := root.create_tween()
    tween.tween_property(root, "scale:x", 1.0, 0.08)
    tween.tween_interval(0.08)
    tween.tween_property(root, "modulate:a", 0.0, 0.12)
    tween.tween_callback(_release_effect.bind(root))

func _spawn_ring(root: Node2D, params: Dictionary) -> void:
    var line := Line2D.new()
    line.closed = true
    line.width = float(params.get("width", 7.0)) * quality_scale
    line.default_color = Color(0.8, 0.9, 1.0, 0.55 if low_flash_mode else 0.9)
    var points := PackedVector2Array()
    var segments := maxi(8, int(24 * quality_scale))
    for index in range(segments):
        var angle := TAU * float(index) / segments
        points.append(Vector2(cos(angle), sin(angle)) * 36.0)
    line.points = points
    root.add_child(line)
    root.scale = Vector2(0.35, 0.35)
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2(1.4, 1.4), 0.18)
    tween.parallel().tween_property(root, "modulate:a", 0.0, 0.18)
    tween.tween_callback(_release_effect.bind(root))

func _release_effect(effect: Node) -> void:
    _active_effects.erase(effect)
    if is_instance_valid(effect):
        effect.queue_free()
