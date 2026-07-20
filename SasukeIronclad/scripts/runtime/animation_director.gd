extends Node
class_name SasukeAnimationDirector

signal timeline_started(animation_id: String, variant: String)
signal impact(animation_id: String, impact_index: int)
signal timeline_completed(animation_id: String)
signal timeline_failed(animation_id: String, reason: String)

@export_file("*.json") var catalog_path := "res://SasukeIronclad/data/timeline_catalog.json"
@export var rig_path: NodePath
@export var vfx_director_path: NodePath
@export var camera_director_path: NodePath
@export var cutin_director_path: NodePath

@onready var rig: SasukeGrayboxRig = get_node(rig_path)
@onready var vfx_director: SasukeVfxDirector = get_node(vfx_director_path)
@onready var camera_director: SasukeCameraEffectDirector = get_node(camera_director_path)
@onready var cutin_director: SasukeCutinDirector = get_node(cutin_director_path)

var _catalog: Dictionary = {}
var _play_generation := 0
var _is_playing := false
var _external_impact_sync := false
var _latest_original_impact := -1

func _ready() -> void:
    _catalog = _load_catalog()

func has_timeline(animation_id: String) -> bool:
    return _catalog.has(animation_id)

func notify_original_impact(impact_index: int) -> void:
    _latest_original_impact = maxi(_latest_original_impact, impact_index)

func cancel_current() -> void:
    _play_generation += 1
    _is_playing = false
    _external_impact_sync = false
    _latest_original_impact = -1
    vfx_director.clear_all()
    camera_director.reset_all()
    cutin_director.clear_all()
    rig.set_pose_immediate("idle_sword_ready")
    rig.set_eye_active(false)

func play_timeline(animation_id: String, variant: String = "base", context: Dictionary = {}) -> bool:
    if _is_playing:
        cancel_current()
    if not _catalog.has(animation_id):
        timeline_failed.emit(animation_id, "timeline_not_registered")
        return false

    var timeline_path: String = _catalog[animation_id]
    var timeline := _load_json(timeline_path)
    if timeline.is_empty():
        timeline_failed.emit(animation_id, "timeline_load_failed")
        return false

    _play_generation += 1
    var generation := _play_generation
    _is_playing = true
    _external_impact_sync = bool(context.get("external_impact_sync", false))
    _latest_original_impact = -1
    var low_flash := variant == "low_flash" or bool(context.get("low_flash", false))
    var speed_scale := _resolve_speed_scale(timeline, variant)
    rig.set_low_flash(low_flash)
    vfx_director.configure(low_flash, float(context.get("quality_scale", 1.0)))
    camera_director.configure(rig, low_flash)
    cutin_director.configure(low_flash)
    timeline_started.emit(animation_id, variant)

    var elapsed_ms := 0
    var impact_index := 0
    for event in timeline.get("events", []):
        if generation != _play_generation:
            return false
        var event_time := int(event.get("time_ms", elapsed_ms))
        var wait_ms := maxi(0, event_time - elapsed_ms)
        if wait_ms > 0:
            await get_tree().create_timer(float(wait_ms) / 1000.0 / speed_scale).timeout
        elapsed_ms = event_time
        if generation != _play_generation:
            return false

        if String(event.get("type", "")) == "impact":
            if _external_impact_sync:
                var timeout_seconds := float(context.get("impact_timeout_seconds", 1.5))
                if not await _wait_for_original_impact(impact_index, generation, timeout_seconds):
                    _is_playing = false
                    timeline_failed.emit(animation_id, "original_impact_timeout")
                    return false
            impact.emit(animation_id, impact_index)
            impact_index += 1
            continue

        _execute_event(event, variant, context)

    var duration_ms := int(timeline.get("duration_ms", elapsed_ms))
    if duration_ms > elapsed_ms:
        await get_tree().create_timer(float(duration_ms - elapsed_ms) / 1000.0 / speed_scale).timeout
    if generation != _play_generation:
        return false

    _is_playing = false
    _external_impact_sync = false
    cutin_director.clear_all()
    await rig.reset_to_idle(0.12 if variant == "fast" else 0.18)
    timeline_completed.emit(animation_id)
    return true

func _wait_for_original_impact(impact_index: int, generation: int, timeout_seconds: float) -> bool:
    var elapsed := 0.0
    while _latest_original_impact < impact_index:
        if generation != _play_generation:
            return false
        if elapsed >= timeout_seconds:
            return false
        var step := minf(0.01, timeout_seconds - elapsed)
        await get_tree().create_timer(step).timeout
        elapsed += step
    return true

func _execute_event(event: Dictionary, variant: String, context: Dictionary) -> void:
    var event_type := String(event.get("type", ""))
    match event_type:
        "pose":
            rig.tween_pose(String(event.get("pose", "idle_sword_ready")), float(event.get("duration_ms", 120)) / 1000.0)
        "eye":
            rig.set_eye_active(bool(event.get("active", true)))
        "vfx":
            if variant == "low_flash" and bool(event.get("suppress_in_low_flash", false)):
                return
            var anchor := rig.get_anchor(String(event.get("anchor", "vfx")))
            vfx_director.spawn_effect(String(event.get("effect", "")), anchor, _resolve_event_params(event, context))
        "camera":
            if variant == "low_flash" and bool(event.get("suppress_in_low_flash", false)):
                return
            if variant == "fast" and bool(event.get("skip_in_fast", false)):
                return
            camera_director.apply_effect(String(event.get("effect", "")), _resolve_event_params(event, context))
        "cutin":
            if variant == "low_flash" and bool(event.get("suppress_in_low_flash", true)):
                return
            if variant == "fast" and bool(event.get("skip_in_fast", true)):
                return
            cutin_director.play_cutin(String(event.get("style", "sharingan_side")), int(event.get("duration_ms", 260)))
        "return_idle":
            rig.reset_to_idle(float(event.get("duration_ms", 160)) / 1000.0)
        _:
            push_warning("Unknown timeline event type: %s" % event_type)

func _resolve_event_params(event: Dictionary, context: Dictionary) -> Dictionary:
    var params: Dictionary = event.get("params", {}).duplicate(true)
    var bindings: Dictionary = event.get("param_bindings", {})
    for param_name in bindings.keys():
        var binding: Dictionary = bindings[param_name]
        var source := String(binding.get("source", ""))
        if source.is_empty() or not context.has(source):
            continue
        var value := float(context[source])
        value = value * float(binding.get("scale", 1.0)) + float(binding.get("offset", 0.0))
        value = clampf(value, float(binding.get("min", -INF)), float(binding.get("max", INF)))
        params[param_name] = int(round(value)) if bool(binding.get("integer", false)) else value
    return params

func _resolve_speed_scale(timeline: Dictionary, variant: String) -> float:
    var variants: Dictionary = timeline.get("variant_overrides", {})
    var override: Dictionary = variants.get(variant, {})
    return maxf(0.25, float(override.get("speed_scale", 1.0)))

func _load_catalog() -> Dictionary:
    var catalog := _load_json(catalog_path)
    var result := {}
    for entry in catalog.get("entries", []):
        result[String(entry.get("animation_id", ""))] = String(entry.get("path", ""))
    return result

func _load_json(path: String) -> Dictionary:
    if not FileAccess.file_exists(path):
        return {}
    var file := FileAccess.open(path, FileAccess.READ)
    if file == null:
        return {}
    var parsed = JSON.parse_string(file.get_as_text())
    return parsed if parsed is Dictionary else {}
