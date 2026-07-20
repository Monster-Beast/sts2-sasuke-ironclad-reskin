extends SasukeCharacterStateDirector
class_name SasukeCharacterStateRuntime

var _runtime_idle_timer: Timer

func _ready() -> void:
    _runtime_idle_timer = Timer.new()
    _runtime_idle_timer.name = "IdleStateTimer"
    _runtime_idle_timer.one_shot = true
    add_child(_runtime_idle_timer)
    super._ready()
    camera_director.configure(rig, false)

func set_card_animation_active(active: bool) -> void:
    _stop_idle_timer()
    super.set_card_animation_active(active)

func cancel_state(force: bool = false) -> void:
    _stop_idle_timer()
    super.cancel_state(force)

func reset_for_combat() -> void:
    _stop_idle_timer()
    super.reset_for_combat()

func release_combat_resources() -> void:
    _stop_idle_timer()
    super.release_combat_resources()

func play_state(state_id: String, params: Dictionary = {}) -> bool:
    var normalized := _normalize_state_id(state_id)
    if _terminal and _current_state == "victory" and normalized == "death" and not bool(params.get("force", false)):
        var forced_params := params.duplicate(true)
        forced_params["force"] = true
        return await super.play_state(normalized, forced_params)
    return await super.play_state(state_id, params)

func _restart_idle_loop() -> void:
    _stop_idle_timer()
    if not _idle_enabled or _terminal or _card_animation_active:
        return
    _idle_generation += 1
    var token := _idle_generation
    _run_idle_loop(token)

func _run_idle_loop(token: int) -> void:
    while token == _idle_generation and _idle_enabled and not _terminal and not _card_animation_active:
        if not await _wait_for_idle_timer(_rng.randf_range(2.8, 5.2), token):
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
        if not await _wait_for_idle_timer(0.70, token):
            return
        rig.set_eye_active(false)
        var return_pose := rig.resolve_character_state_pose("idle_sword_ready")
        rig.tween_pose(return_pose, 0.18)
        _current_state = return_pose

func _wait_for_idle_timer(seconds: float, token: int) -> bool:
    if not is_instance_valid(_runtime_idle_timer) or token != _idle_generation:
        return false
    _runtime_idle_timer.start(maxf(0.01, seconds))
    await _runtime_idle_timer.timeout
    return token == _idle_generation and _idle_enabled and not _terminal and not _card_animation_active

func _stop_idle_timer() -> void:
    if is_instance_valid(_runtime_idle_timer):
        _runtime_idle_timer.stop()
