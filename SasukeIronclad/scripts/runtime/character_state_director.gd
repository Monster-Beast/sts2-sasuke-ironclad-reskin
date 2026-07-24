extends Node
class_name SasukeCharacterStateDirector

signal state_started(state_id: String)
signal state_completed(state_id: String)
signal state_rejected(state_id: String, reason: String)

@export var rig_path: NodePath
@export var vfx_director_path: NodePath
@export var advanced_vfx_director_path: NodePath
@export var camera_director_path: NodePath

@onready var rig: SasukeCharacterStateRig = get_node(rig_path)
@onready var vfx_director: SasukeVfxDirector = get_node(vfx_director_path)
@onready var advanced_vfx_director: SasukeAdvancedVfxDirector = get_node(advanced_vfx_director_path)
@onready var camera_director: SasukeCameraEffectDirector = get_node(camera_director_path)

const PRIORITY := {
    "idle_neutral": 0,
    "idle_sword_ready": 0,
    "idle_sharingan_alert": 0,
    "combat_entry": 10,
    "hit_light": 30,
    "hit_heavy": 40,
    "victory": 80,
    "death": 100
}

var _generation := 0
var _idle_generation := 0
var _current_state := ""
var _terminal := false
var _card_animation_active := false
var _idle_enabled := true
var _rig_origin := Vector2.ZERO
var _rng := RandomNumberGenerator.new()

func _ready() -> void:
    _rig_origin = rig.position
    _rng.seed = 0x5A5A2026
    call_deferred("_restart_idle_loop")

func set_card_animation_active(active: bool) -> void:
    _card_animation_active = active
    _idle_generation += 1
    if not active and _idle_enabled and not _terminal:
        _restart_idle_loop()

func play_state(state_id: String, params: Dictionary = {}) -> bool:
    var normalized := _normalize_state_id(state_id)
    if not PRIORITY.has(normalized):
        state_rejected.emit(normalized, "unknown_state")
        return false

    var force := bool(params.get("force", false))
    if _terminal and not force:
        state_rejected.emit(normalized, "terminal_state_locked")
        return false

    var current_priority := int(PRIORITY.get(_current_state, -1))
    var requested_priority := int(PRIORITY[normalized])
    if not force and current_priority > requested_priority:
        state_rejected.emit(normalized, "lower_priority")
        return false

    _generation += 1
    var generation := _generation
    _idle_generation += 1
    _current_state = normalized
    state_started.emit(normalized)

    var completed := false
    match normalized:
        "combat_entry": completed = await _play_entry(generation, params)
        "hit_light": completed = await _play_hit(generation, false, params)
        "hit_heavy": completed = await _play_hit(generation, true, params)
        "death": completed = await _play_death(generation, params)
        "victory": completed = await _play_victory(generation, params)
        _: completed = await _play_idle_state(generation, normalized)

    if generation != _generation:
        return false
    if completed:
        state_completed.emit(normalized)
    return completed

func cancel_state(force: bool = false) -> void:
    if _terminal and not force:
        return
    _generation += 1
    _idle_generation += 1
    _terminal = false
    _current_state = ""
    rig.position = _rig_origin
    rig.set_eye_active(false)
    rig.set_pose_immediate(rig.resolve_character_state_pose("idle_sword_ready"))
    if _idle_enabled and not _card_animation_active:
        _restart_idle_loop()

func reset_for_combat() -> void:
    cancel_state(true)
    _idle_enabled = true
    _terminal = false
    _current_state = rig.resolve_character_state_pose("idle_sword_ready")
    _restart_idle_loop()

func release_combat_resources() -> void:
    _generation += 1
    _idle_generation += 1
    _idle_enabled = false
    _terminal = false
    _card_animation_active = false
    _current_state = ""
    rig.position = _rig_origin
    rig.set_eye_active(false)
    rig.set_pose_immediate("idle_sword_ready")

func current_state_id() -> String:
    return _current_state

func is_terminal() -> bool:
    return _terminal

func is_busy() -> bool:
    return not _current_state.is_empty() and int(PRIORITY.get(_current_state, 0)) > 0

func _play_entry(generation: int, params: Dictionary) -> bool:
    _terminal = false
    rig.set_eye_active(false)
    rig.set_pose_immediate("combat_entry_start")
    rig.position = _rig_origin + Vector2(-220.0, 0.0)
    advanced_vfx_director.spawn_effect("body_flicker_trail", rig.get_anchor("vfx"), {
        "copies": 5,
        "distance": 110.0
    })
    var travel := rig.create_tween()
    travel.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
    travel.tween_property(rig, "position", _rig_origin, 0.24)
    rig.tween_pose("combat_entry_land", 0.22)
    await travel.finished
    if generation != _generation:
        return false
    camera_director.apply_effect("shake_light", {"amplitude": 3.0, "duration": 0.10})
    await get_tree().create_timer(0.10).timeout
    return await _return_to_idle(generation)

func _play_hit(generation: int, heavy: bool, params: Dictionary) -> bool:
    var state_id := "hit_heavy" if heavy else "hit_light"
    var pose := rig.resolve_character_state_pose(state_id)
    var intensity := clampf(float(params.get("intensity", 1.0)), 0.4, 2.0)
    rig.set_eye_active(false)
    rig.tween_pose(pose, 0.10 if heavy else 0.07)
    vfx_director.spawn_effect("short_impact_ring", rig.get_anchor("vfx"), {
        "width": (10.0 if heavy else 6.0) * intensity
    })
    camera_director.apply_effect(
        "shake_medium" if heavy else "shake_light",
        {
            "amplitude": (8.0 if heavy else 3.5) * intensity,
            "duration": 0.18 if heavy else 0.10
        }
    )
    await get_tree().create_timer(0.24 if heavy else 0.14).timeout
    if generation != _generation:
        return false
    return await _return_to_idle(generation)

func _play_death(generation: int, params: Dictionary) -> bool:
    _terminal = true
    _idle_enabled = false
    _card_animation_active = false
    rig.set_eye_active(false)
    rig.tween_pose(rig.resolve_character_state_pose("death_ready"), 0.20)
    camera_director.apply_effect("scene_dim", {"opacity": 0.34, "duration": 0.36})
    await get_tree().create_timer(0.24).timeout
    if generation != _generation:
        return false
    if rig.active_visual_form_id() == "curse_mark_stage_two":
        advanced_vfx_director.spawn_effect("curse_mark_consume", rig.get_anchor("vfx"), {
            "radius": 112.0,
            "spokes": 16
        })
    rig.tween_pose(rig.resolve_character_state_pose("death"), 0.34)
    await get_tree().create_timer(0.38).timeout
    if generation != _generation:
        return false
    _current_state = "death"
    return true

func _play_victory(generation: int, params: Dictionary) -> bool:
    _terminal = true
    _idle_enabled = false
    _card_animation_active = false
    rig.set_eye_active(false)
    rig.tween_pose(rig.resolve_character_state_pose("victory_sheathe"), 0.34)
    vfx_director.spawn_effect("minor_blue_sparks", rig.get_anchor("weapon"), {
        "count": 8
    })
    await get_tree().create_timer(0.40).timeout
    if generation != _generation:
        return false
    _current_state = "victory"
    return true

func _play_idle_state(generation: int, state_id: String) -> bool:
    if _terminal or _card_animation_active:
        return false
    var pose := rig.resolve_character_state_pose(state_id)
    rig.set_eye_active(state_id == "idle_sharingan_alert")
    await rig.tween_pose(pose, 0.20)
    if generation != _generation:
        return false
    _current_state = pose
    return true

func _return_to_idle(generation: int) -> bool:
    if generation != _generation or _terminal:
        return false
    rig.set_eye_active(false)
    var idle_pose := rig.resolve_character_state_pose("idle_sword_ready")
    await rig.tween_pose(idle_pose, 0.16)
    if generation != _generation:
        return false
    _current_state = idle_pose
    if _idle_enabled and not _card_animation_active:
        _restart_idle_loop()
    return true

func _restart_idle_loop() -> void:
    if not _idle_enabled or _terminal or _card_animation_active:
        return
    _idle_generation += 1
    var token := _idle_generation
    _run_idle_loop(token)

func _run_idle_loop(token: int) -> void:
    while token == _idle_generation and _idle_enabled and not _terminal and not _card_animation_active:
        await get_tree().create_timer(_rng.randf_range(2.8, 5.2)).timeout
        if token != _idle_generation or _terminal or _card_animation_active:
            return
        var roll := _rng.randf()
        var idle_state := "idle_sword_ready"
        if roll < 0.15:
            idle_state = "idle_sharingan_alert"
        elif roll < 0.42:
            idle_state = "idle_neutral"
        var pose := rig.resolve_character_state_pose(idle_state)
        rig.set_eye_active(idle_state == "idle_sharingan_alert")
        rig.tween_pose(pose, 0.22)
        _current_state = pose
        await get_tree().create_timer(0.70).timeout
        if token != _idle_generation or _terminal or _card_animation_active:
            return
        rig.set_eye_active(false)
        var return_pose := rig.resolve_character_state_pose("idle_sword_ready")
        rig.tween_pose(return_pose, 0.18)
        _current_state = return_pose

func _normalize_state_id(state_id: String) -> String:
    match state_id.strip_edges().to_lower():
        "entry", "combat_entry", "combat-entry": return "combat_entry"
        "light_hit", "hit_light", "hit-light": return "hit_light"
        "heavy_hit", "hit_heavy", "hit-heavy": return "hit_heavy"
        "win", "victory", "victory_sheathe": return "victory"
        "die", "death": return "death"
        "idle_neutral": return "idle_neutral"
        "idle_sharingan_alert": return "idle_sharingan_alert"
        _: return "idle_sword_ready" if state_id == "idle_sword_ready" else state_id
