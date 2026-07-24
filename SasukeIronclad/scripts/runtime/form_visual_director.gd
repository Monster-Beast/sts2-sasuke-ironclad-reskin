extends Node
class_name SasukeFormVisualDirector

@export var rig_path: NodePath
@onready var rig: SasukeTransformableRig = get_node(rig_path)

var _committed_form_id := ""
var _committed_params: Dictionary = {}
var _pending_by_generation: Dictionary = {}

func install_form(form_id: String, params: Dictionary, generation: int) -> void:
    if form_id.is_empty():
        push_warning("Cannot install an unnamed visual form")
        return
    if not _pending_by_generation.has(generation):
        _pending_by_generation[generation] = {
            "previous_form_id": _committed_form_id,
            "previous_params": _committed_params.duplicate(true)
        }
    var pending: Dictionary = _pending_by_generation[generation]
    pending["form_id"] = form_id
    pending["params"] = params.duplicate(true)
    _pending_by_generation[generation] = pending
    rig.apply_visual_form(form_id, params)

func commit_generation(generation: int) -> void:
    if not _pending_by_generation.has(generation):
        return
    var pending: Dictionary = _pending_by_generation[generation]
    _committed_form_id = String(pending.get("form_id", ""))
    _committed_params = (pending.get("params", {}) as Dictionary).duplicate(true)
    _pending_by_generation.erase(generation)

func rollback_generation(generation: int) -> void:
    if not _pending_by_generation.has(generation):
        return
    var pending: Dictionary = _pending_by_generation[generation]
    var previous_form_id := String(pending.get("previous_form_id", ""))
    var previous_params: Dictionary = (pending.get("previous_params", {}) as Dictionary).duplicate(true)
    _pending_by_generation.erase(generation)
    if previous_form_id.is_empty():
        rig.clear_visual_form()
    else:
        rig.apply_visual_form(previous_form_id, previous_params)

func clear_form(form_id: String = "") -> void:
    if not form_id.is_empty() and form_id != _committed_form_id and form_id != rig.active_visual_form_id():
        return
    _committed_form_id = ""
    _committed_params.clear()
    _pending_by_generation.clear()
    rig.clear_visual_form(form_id)

func clear_all() -> void:
    _committed_form_id = ""
    _committed_params.clear()
    _pending_by_generation.clear()
    rig.clear_visual_form()

func active_form_id() -> String:
    return rig.active_visual_form_id()

func has_form(form_id: String) -> bool:
    return not form_id.is_empty() and rig.active_visual_form_id() == form_id
