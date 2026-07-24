extends SasukeAnimationDirector
class_name SasukeStatefulAnimationDirector

signal playback_committed(animation_id: String)

@export var state_visual_director_path: NodePath
@export var form_visual_director_path: NodePath
@export var character_state_director_path: NodePath
@onready var state_visual_director: SasukeStateVisualDirector = get_node(state_visual_director_path)
@onready var form_visual_director: SasukeFormVisualDirector = get_node(form_visual_director_path)
@onready var character_state_director: SasukeCharacterStateDirector = get_node(character_state_director_path)

var _transaction_counter := 0
var _active_transaction := -1
var _runtime_anchor: Node2D

func bind_runtime_anchor(anchor: Node2D) -> bool:
    clear_runtime_anchor()
    if not is_instance_valid(anchor) or not anchor.is_inside_tree():
        return false
    _runtime_anchor = anchor
    anchor.tree_exiting.connect(_on_runtime_anchor_tree_exiting, CONNECT_ONE_SHOT)
    var runtime_root := get_parent() as CanvasItem
    if runtime_root != null:
        runtime_root.visible = true
    return true

func clear_runtime_anchor() -> void:
    var anchor := _runtime_anchor
    _runtime_anchor = null
    if is_instance_valid(anchor) and anchor.tree_exiting.is_connected(_on_runtime_anchor_tree_exiting):
        anchor.tree_exiting.disconnect(_on_runtime_anchor_tree_exiting)

func _on_runtime_anchor_tree_exiting() -> void:
    _runtime_anchor = null
    release_combat_resources()
    var runtime_root := get_parent() as CanvasItem
    if runtime_root == null:
        return
    runtime_root.visible = false
    var host := runtime_root.get_parent()
    if is_instance_valid(host):
        host.set_meta("sasuke_runtime_anchor_exited", true)
        if host is CanvasItem:
            host.visible = false
    print("[SasukeIronclad] runtime anchor exited; GDScript released and hid combat visuals.")

func play_timeline(animation_id: String, variant: String = "base", context: Dictionary = {}) -> bool:
    if character_state_director.is_terminal():
        timeline_failed.emit(animation_id, "character_terminal")
        return false

    if _is_playing:
        cancel_current()

    _transaction_counter += 1
    var transaction := _transaction_counter
    _active_transaction = transaction

    character_state_director.set_card_animation_active(true)
    var low_flash := variant == "low_flash" or bool(context.get("low_flash", false))
    state_visual_director.configure(low_flash, float(context.get("quality_scale", 1.0)))

    var completed := await super.play_timeline(animation_id, variant, context)
    if completed:
        state_visual_director.commit_generation(transaction)
        form_visual_director.commit_generation(transaction)
        # The base timeline_completed signal is intentionally not used by the
        # C# host for this derived director. This signal is emitted only after
        # persistent visual transactions are committed.
        playback_committed.emit(animation_id)
    else:
        state_visual_director.rollback_generation(transaction)
        form_visual_director.rollback_generation(transaction)

    if _active_transaction == transaction:
        _active_transaction = -1
        character_state_director.set_card_animation_active(false)
    return completed

func cancel_current() -> void:
    var transaction := _active_transaction
    _active_transaction = -1
    if transaction >= 0:
        state_visual_director.rollback_generation(transaction)
        form_visual_director.rollback_generation(transaction)
    character_state_director.set_card_animation_active(false)
    super.cancel_current()
    rig.set_pose_immediate(rig.resolve_character_state_pose("idle_sword_ready"))

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
    clear_runtime_anchor()
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
            if _active_transaction < 0:
                push_warning("Ignoring state_install without an active visual transaction")
                return
            state_visual_director.install_state(
                String(event.get("state_id", "")),
                String(event.get("style", "curse_channel")),
                rig.get_anchor(String(event.get("anchor", "vfx"))),
                _resolve_event_params(event, context),
                _active_transaction
            )
        "state_pulse":
            state_visual_director.pulse_state(
                String(event.get("state_id", "")),
                _resolve_event_params(event, context)
            )
        "state_clear":
            state_visual_director.clear_state(String(event.get("state_id", "")))
        "form_install":
            if _active_transaction < 0:
                push_warning("Ignoring form_install without an active visual transaction")
                return
            form_visual_director.install_form(
                String(event.get("form_id", "")),
                _resolve_event_params(event, context),
                _active_transaction
            )
        "form_clear":
            clear_visual_form(String(event.get("form_id", "")))
        _:
            super._execute_event(event, variant, context)
