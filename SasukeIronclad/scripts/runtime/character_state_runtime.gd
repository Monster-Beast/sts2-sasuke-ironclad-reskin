extends SasukeCharacterStateDirector
class_name SasukeCharacterStateRuntime

func _ready() -> void:
    super._ready()
    camera_director.configure(rig, false)

func play_state(state_id: String, params: Dictionary = {}) -> bool:
    var normalized := _normalize_state_id(state_id)
    if _terminal and _current_state == "victory" and normalized == "death" and not bool(params.get("force", false)):
        var forced_params := params.duplicate(true)
        forced_params["force"] = true
        return await super.play_state(normalized, forced_params)
    return await super.play_state(state_id, params)

func _restart_idle_loop() -> void:
    if not _idle_enabled or _terminal or _card_animation_active:
        return
    _idle_generation += 1
    var token := _idle_generation
    _run_idle_loop(token)

func _run_idle_loop(token: int) -> void:
    while token == _idle_generation and _idle_enabled and not _terminal and not _card_animation_active:
        if not await _wait_for_idle_deadline(_rng.randf_range(2.8, 5.2), token):
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
        if not await _wait_for_idle_deadline(0.70, token):
            return
        rig.set_eye_active(false)
        var return_pose := rig.resolve_character_state_pose("idle_sword_ready")
        rig.tween_pose(return_pose, 0.18)
        _current_state = return_pose

func _wait_for_idle_deadline(seconds: float, token: int) -> bool:
    var deadline_ms := Time.get_ticks_msec() + int(maxf(0.01, seconds) * 1000.0)
    while token == _idle_generation and _idle_enabled and not _terminal and not _card_animation_active:
        if Time.get_ticks_msec() >= deadline_ms:
            return true
        await get_tree().process_frame
    return false
