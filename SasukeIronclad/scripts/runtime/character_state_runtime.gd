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
