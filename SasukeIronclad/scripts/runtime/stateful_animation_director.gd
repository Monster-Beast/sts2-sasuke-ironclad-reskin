extends SasukeAnimationDirector
class_name SasukeStatefulAnimationDirector

@export var state_visual_director_path: NodePath
@export var form_visual_director_path: NodePath
@export var character_state_director_path: NodePath
@onready var state_visual_director: SasukeStateVisualDirector = get_node(state_visual_director_path)
@onready var form_visual_director: SasukeFormVisualDirector = get_node(form_visual_director_path)
@onready var character_state_director: SasukeCharacterStateDirector = get_node(character_state_director_path)

func play_timeline(animation_id: String, variant: String = "base", context: Dictionary = {}) -> bool:
    if character_state_director.is_terminal():
        timeline_failed.emit(animation_id, "character_terminal")
        return false

    character_state_director.set_card_animation_active(true)
    var low_flash := variant == "low_flash" or bool(context.get("low_flash", false))
    state_visual_director.configure(low_flash, float(context.get("quality_scale", 1.0)))
    var completed := await super.play_timeline(animation_id, variant, context)
    character_state_director.set_card_animation_active(false)
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
    character_state_director.set_card_animation_active(false)
    super.cancel_current()

func play_character_state(state_id: String, params: Dictionary = {}) -> bool:
    var normalized := state_id.strip_edges().to_lower()
    if normalized in ["death", "die", "victory", "win", "victory_sheathe"]:
        cancel_current()
    return await character_state_director.play_state(state_id, params)

func reset_character_state_machine() -> void:
    character_state_director.reset_for_combat()

func cancel_character_state(force: bool = false) -> void:
    character_state_director.cancel_state(force)

func current_character_state_id() -> String:
    return character_state_director.current_state_id()

func is_character_terminal() -> bool:
    return character_state_director.is_terminal()

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
    character_state_director.release_combat_resources()
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
