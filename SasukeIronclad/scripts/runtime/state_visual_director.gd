extends Node2D
class_name SasukeStateVisualDirector

var low_flash_mode := false
var quality_scale := 1.0
var _states: Dictionary = {}
var _pending_by_generation: Dictionary = {}

func _process(delta: float) -> void:
    var now := float(Time.get_ticks_msec()) / 1000.0
    for state_id in _states.keys():
        var entry: Dictionary = _states[state_id]
        var root: Node2D = entry.get("node")
        if not is_instance_valid(root) or root.is_queued_for_deletion():
            continue
        root.rotation += float(entry.get("rotation_speed", 0.0)) * delta
        var pulse := float(entry.get("pulse", 0.0))
        if pulse > 0.0:
            var scale_value := 1.0 + sin(now * float(entry.get("pulse_speed", 3.0))) * pulse
            root.scale = Vector2.ONE * scale_value

func configure(low_flash: bool, quality: float = 1.0) -> void:
    low_flash_mode = low_flash
    quality_scale = clampf(quality, 0.25, 1.0)

func install_state(
    state_id: String,
    style: String,
    anchor: Node2D,
    params: Dictionary,
    generation: int
) -> Node2D:
    if state_id.is_empty() or not is_instance_valid(anchor):
        push_warning("Cannot install an unnamed visual state or use an invalid anchor")
        return Node2D.new()

    if not _pending_by_generation.has(generation):
        _pending_by_generation[generation] = {}
    var pending: Dictionary = _pending_by_generation[generation]

    # Snapshot the previously committed state only once. Reinstalling the same
    # state inside one timeline must still roll back to the state that existed
    # before that timeline began.
    if not pending.has(state_id):
        var snapshot := {"had_previous": false}
        if _states.has(state_id):
            var previous: Dictionary = _states[state_id]
            if bool(previous.get("committed", false)):
                snapshot = {
                    "had_previous": true,
                    "style": String(previous.get("style", "curse_channel")),
                    "params": (previous.get("params", {}) as Dictionary).duplicate(true),
                    "anchor": previous.get("anchor")
                }
        pending[state_id] = snapshot
        _pending_by_generation[generation] = pending

    _erase_state_node(state_id)
    return _create_state(state_id, style, anchor, params, generation, false)

func commit_generation(generation: int) -> void:
    if not _pending_by_generation.has(generation):
        return
    var pending: Dictionary = _pending_by_generation[generation]
    for state_id in pending.keys():
        if not _states.has(state_id):
            continue
        var entry: Dictionary = _states[state_id]
        if int(entry.get("generation", -1)) == generation:
            entry["committed"] = true
            _states[state_id] = entry
    _pending_by_generation.erase(generation)

func rollback_generation(generation: int) -> void:
    if not _pending_by_generation.has(generation):
        return
    var pending: Dictionary = _pending_by_generation[generation]
    _pending_by_generation.erase(generation)

    for state_id_variant in pending.keys():
        var state_id := String(state_id_variant)
        if _states.has(state_id):
            var current: Dictionary = _states[state_id]
            if int(current.get("generation", -1)) == generation and not bool(current.get("committed", false)):
                _erase_state_node(state_id)

        var snapshot: Dictionary = pending[state_id]
        if not bool(snapshot.get("had_previous", false)):
            continue
        var previous_anchor: Node2D = snapshot.get("anchor")
        if not is_instance_valid(previous_anchor):
            continue
        _create_state(
            state_id,
            String(snapshot.get("style", "curse_channel")),
            previous_anchor,
            (snapshot.get("params", {}) as Dictionary).duplicate(true),
            -1,
            true
        )

func pulse_state(state_id: String, params: Dictionary = {}) -> bool:
    if not _states.has(state_id):
        return false
    var entry: Dictionary = _states[state_id]
    var root: Node2D = entry.get("node")
    if not is_instance_valid(root):
        _states.erase(state_id)
        return false

    var scale_value := clampf(float(params.get("scale", 1.16)), 1.0, 1.5)
    var opacity := clampf(float(params.get("opacity", 1.0)), 0.2, 1.0)
    var duration := clampf(float(params.get("duration", 0.20)), 0.06, 0.8)
    if low_flash_mode:
        scale_value = minf(scale_value, 1.08)
        opacity = minf(opacity, 0.70)
        duration = maxf(duration, 0.16)

    root.modulate.a = opacity
    var tween := root.create_tween()
    tween.tween_property(root, "scale", Vector2.ONE * scale_value, duration * 0.38)
    tween.tween_property(root, "scale", Vector2.ONE, duration * 0.62)
    tween.parallel().tween_property(root, "modulate:a", 1.0, duration * 0.62)
    return true

func clear_state(state_id: String) -> void:
    _erase_state_node(state_id)
    var empty_generations: Array = []
    for generation in _pending_by_generation.keys():
        var pending: Dictionary = _pending_by_generation[generation]
        pending.erase(state_id)
        if pending.is_empty():
            empty_generations.append(generation)
        else:
            _pending_by_generation[generation] = pending
    for generation in empty_generations:
        _pending_by_generation.erase(generation)

func clear_all() -> void:
    var state_ids := _states.keys().duplicate()
    for state_id in state_ids:
        _erase_state_node(String(state_id))
    _states.clear()
    _pending_by_generation.clear()

func active_state_count() -> int:
    var total := 0
    for entry in _states.values():
        var root: Node = (entry as Dictionary).get("node")
        if is_instance_valid(root) and not root.is_queued_for_deletion():
            total += 1
    return total

func has_state(state_id: String) -> bool:
    if not _states.has(state_id):
        return false
    var root: Node = (_states[state_id] as Dictionary).get("node")
    return is_instance_valid(root) and not root.is_queued_for_deletion()

func _create_state(
    state_id: String,
    style: String,
    anchor: Node2D,
    params: Dictionary,
    generation: int,
    committed: bool
) -> Node2D:
    var root := Node2D.new()
    root.name = "StateVFX_%s" % state_id
    anchor.add_child(root)
    root.position = Vector2.ZERO
    _build_state(root, style, params)
    _states[state_id] = {
        "node": root,
        "style": style,
        "params": params.duplicate(true),
        "anchor": anchor,
        "generation": generation,
        "committed": committed,
        "rotation_speed": float(params.get("rotation_speed", 0.0)),
        "pulse": float(params.get("pulse", 0.025)),
        "pulse_speed": float(params.get("pulse_speed", 3.0))
    }
    return root

func _erase_state_node(state_id: String) -> void:
    if not _states.has(state_id):
        return
    var root: Node = (_states[state_id] as Dictionary).get("node")
    _states.erase(state_id)
    if is_instance_valid(root):
        root.queue_free()

func _build_state(root: Node2D, style: String, params: Dictionary) -> void:
    match style:
        "fire_guard": _build_fire_guard(root, params)
        "curse_channel": _build_curse_channel(root, params)
        "curse_overdrive": _build_curse_overdrive(root, params)
        _: _build_curse_channel(root, params)

func _build_fire_guard(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 92.0))
    var layers := clampi(int(params.get("layers", 2)), 1, 3)
    for layer in range(layers):
        var arc := Line2D.new()
        arc.width = maxf(3.0, (9.0 - layer * 2.0) * quality_scale)
        arc.default_color = Color(1.0, 0.30 + layer * 0.08, 0.08, 0.42 if low_flash_mode else 0.78)
        arc.antialiased = true
        var points := PackedVector2Array()
        var segments := maxi(12, int(28 * quality_scale))
        var layer_radius := radius + float(layer) * 16.0
        for index in range(segments + 1):
            var angle := lerpf(-2.85, -0.28, float(index) / segments)
            points.append(Vector2(cos(angle), sin(angle)) * layer_radius + Vector2(18, -10))
        arc.points = points
        root.add_child(arc)

    var embers := clampi(int(params.get("embers", 8)), 3, 16)
    for index in range(embers):
        var ember := Line2D.new()
        var angle := lerpf(-2.70, -0.40, float(index) / maxf(1.0, embers - 1.0))
        var start := Vector2(cos(angle), sin(angle)) * radius
        ember.points = PackedVector2Array([start, start + Vector2(8, -18 - float(index % 3) * 4.0)])
        ember.width = 2.0 * quality_scale
        ember.default_color = Color(1.0, 0.56, 0.12, 0.30 if low_flash_mode else 0.66)
        root.add_child(ember)

func _build_curse_channel(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 70.0))
    var marks := clampi(int(params.get("marks", 7)), 4, 12)
    for index in range(marks):
        var mark := Line2D.new()
        var angle := TAU * float(index) / marks
        var start := Vector2(cos(angle), sin(angle) * 0.65) * radius * 0.62
        var middle := Vector2(cos(angle + 0.20), sin(angle + 0.20) * 0.65) * radius * 0.86
        var finish := Vector2(cos(angle - 0.10), sin(angle - 0.10) * 0.65) * radius
        mark.points = PackedVector2Array([start, middle, finish])
        mark.width = 3.4 * quality_scale
        mark.default_color = Color(0.50, 0.20, 0.72, 0.32 if low_flash_mode else 0.72)
        root.add_child(mark)

func _build_curse_overdrive(root: Node2D, params: Dictionary) -> void:
    _build_curse_channel(root, params)
    var radius := float(params.get("radius", 88.0))
    var spokes := clampi(int(params.get("spokes", 10)), 6, 18)
    for index in range(spokes):
        var spoke := Line2D.new()
        var angle := TAU * float(index) / spokes
        spoke.points = PackedVector2Array([
            Vector2(cos(angle), sin(angle) * 0.60) * radius * 0.78,
            Vector2(cos(angle), sin(angle) * 0.60) * radius * 1.15
        ])
        spoke.width = 2.2 * quality_scale
        spoke.default_color = Color(0.72, 0.22, 0.84, 0.30 if low_flash_mode else 0.68)
        root.add_child(spoke)
