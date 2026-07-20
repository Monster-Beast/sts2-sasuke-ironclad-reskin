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
        "indigo_guard_arc": _spawn_arc(effect, Color(0.30, 0.38, 0.82, 0.7), params)
        "wire_glint": _spawn_wire(effect)
        "minor_blue_sparks": _spawn_sparks(effect, params)
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
