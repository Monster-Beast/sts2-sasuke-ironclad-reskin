extends SasukeAnimationDirector
class_name SasukeStatefulAnimationDirector

@export var state_visual_director_path: NodePath
@export var form_visual_director_path: NodePath
@onready var state_visual_director: SasukeStateVisualDirector = get_node(state_visual_director_path)
@onready var form_visual_director: SasukeFormVisualDirector = get_node(form_visual_director_path)

func play_timeline(animation_id: String, variant: String = "base", context: Dictionary = {}) -> bool:
    var low_flash := variant == "low_flash" or bool(context.get("low_flash", false))
    state_visual_director.configure(low_flash, float(context.get("quality_scale", 1.0)))
    var completed := await super.play_timeline(animation_id, variant, context)
    if completed:
        state_visual_director.commit_generation(_play_generation)
        form_visual_director.commit_generation(_play_generation)
    else:
        state_visual_director.rollback_generation(_play_generation)
        form_visual_director.rollback_generation(_play_generation)
    return completed

func cancel_current() -> void:
    state_visual_director.rollback_generation(_play_generation)
    form_visual_director.rollback_generation(_play_generation)
    super.cancel_current()

func pulse_visual_state(state_id: String, params: Dictionary = {}) -> bool:
    return state_visual_director.pulse_state(state_id, params)

func clear_visual_state(state_id: String) -> void:
    state_visual_director.clear_state(state_id)

func clear_all_visual_states() -> void:
    state_visual_director.clear_all()

func active_visual_state_count() -> int:
    return state_visual_director.active_state_count()

func has_visual_state(state_id: String) -> bool:
    return state_visual_director.has_state(state_id)

func clear_visual_form(form_id: String = "") -> void:
    form_visual_director.clear_form(form_id)
    if form_id.is_empty() or form_id == "curse_mark_stage_two":
        state_visual_director.clear_state("demon_form_stage_two_aura")
    rig.reset_to_idle(0.14)

func active_visual_form_id() -> String:
    return form_visual_director.active_form_id()

func has_visual_form(form_id: String) -> bool:
    return form_visual_director.has_form(form_id)

func release_combat_resources() -> void:
    cancel_current()
    state_visual_director.clear_all()
    form_visual_director.clear_all()
    vfx_director.clear_all()
    advanced_vfx_director.clear_all()
    camera_director.reset_all()
    cutin_director.clear_all()
    rig.set_pose_immediate("idle_sword_ready")

func _execute_event(event: Dictionary, variant: String, context: Dictionary) -> void:
    var event_type := String(event.get("type", ""))
    match event_type:
        "state_install":
            state_visual_director.install_state(
                String(event.get("state_id", "")),
                String(event.get("style", "curse_channel")),
                rig.get_anchor(String(event.get("anchor", "vfx"))),
                _resolve_event_params(event, context),
                _play_generation
            )
        "state_pulse":
            state_visual_director.pulse_state(
                String(event.get("state_id", "")),
                _resolve_event_params(event, context)
            )
        "state_clear":
            state_visual_director.clear_state(String(event.get("state_id", "")))
        "form_install":
            form_visual_director.install_form(
                String(event.get("form_id", "")),
                _resolve_event_params(event, context),
                _play_generation
            )
        "form_clear":
            clear_visual_form(String(event.get("form_id", "")))
        _:
            super._execute_event(event, variant, context)
