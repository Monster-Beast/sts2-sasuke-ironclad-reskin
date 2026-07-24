extends Node

@onready var runtime: Node2D = $SasukeAnimationRuntime
@onready var director: SasukeStatefulAnimationDirector = $SasukeAnimationRuntime/AnimationDirector
@onready var cutin_root: Control = $SasukeAnimationRuntime/CutinDirector/Root

var failures: Array[String] = []

func _ready() -> void:
    await get_tree().process_frame
    var context := {
        "low_flash": false,
        "fast_mode": true,
        "quality_scale": 0.5,
        "strength": 10,
        "target_count": 3,
        "energy_spent": 3,
        "exhausted_card_count": 1,
        "hit_count": 1
    }

    var flame_completed := await director.play_timeline("flame_barrier_uchiha_fire_guard", "base", context)
    _expect(flame_completed, "Flame Barrier did not complete")
    _expect(director.has_visual_state("flame_barrier_guard"), "Flame Barrier state was not committed")
    _expect(director.active_visual_state_count() == 1, "unexpected state count after Flame Barrier")

    director.play_timeline("flame_barrier_uchiha_fire_guard", "base", context)
    await get_tree().create_timer(0.32).timeout
    director.cancel_current()
    await get_tree().process_frame
    await get_tree().process_frame
    _expect(director.has_visual_state("flame_barrier_guard"), "cancelled replacement removed the previous Flame Barrier state")
    _expect(director.active_visual_state_count() == 1, "cancelled replacement duplicated or lost Flame Barrier state")

    var strike_completed := await director.play_timeline("strike_kusanagi_draw_slash", "base", context)
    _expect(strike_completed, "Strike did not complete after persistent state installation")
    _expect(director.has_visual_state("flame_barrier_guard"), "a later card incorrectly cleared Flame Barrier")
    _expect(director.pulse_visual_state("flame_barrier_guard", {"scale": 1.12, "duration": 0.12}), "Flame Barrier pulse failed")

    director.clear_visual_state("flame_barrier_guard")
    await get_tree().process_frame
    await get_tree().process_frame
    _expect(director.active_visual_state_count() == 0, "explicit state clear did not release Flame Barrier")

    director.play_timeline("limit_break_sharingan_curse_overdrive", "base", context)
    await get_tree().create_timer(0.46).timeout
    director.cancel_current()
    await get_tree().process_frame
    await get_tree().process_frame
    _expect(not director.has_visual_state("limit_break_overdrive"), "cancelled Limit Break left a pending state")

    var limit_completed := await director.play_timeline("limit_break_sharingan_curse_overdrive", "base", context)
    _expect(limit_completed, "Limit Break did not complete")
    _expect(director.has_visual_state("limit_break_overdrive"), "Limit Break state was not committed")

    var pact_completed := await director.play_timeline("burning_pact_curse_seal_consumption", "base", context)
    _expect(pact_completed, "Burning Pact did not complete")
    _expect(not director.has_visual_state("burning_pact_channel"), "Burning Pact temporary channel state remained")
    _expect(director.has_visual_state("limit_break_overdrive"), "Burning Pact incorrectly cleared committed Limit Break state")

    director.release_combat_resources()
    await get_tree().process_frame
    await get_tree().process_frame
    await get_tree().create_timer(0.05).timeout
    _expect(director.active_visual_state_count() == 0, "combat release left persistent states")
    _expect(_count_runtime_visual_nodes(runtime) == 0, "combat release left temporary visual nodes")
    _expect(not cutin_root.visible, "combat release left Cut-in visible")

    runtime.queue_free()
    await get_tree().process_frame
    await get_tree().process_frame
    await get_tree().create_timer(0.05).timeout

    if failures.is_empty():
        print("RELEASE_OK states=0 transients=0")
        get_tree().quit(0)
        return

    for failure in failures:
        push_error(failure)
    push_error("RELEASE_FAILED failures=%d" % failures.size())
    get_tree().quit(1)

func _expect(condition: bool, message: String) -> void:
    if not condition:
        failures.append(message)

func _count_runtime_visual_nodes(node: Node) -> int:
    var total := 0
    var node_name := String(node.name)
    if node_name.begins_with("VFX_") or node_name.begins_with("AdvancedVFX_") or node_name.begins_with("StateVFX_"):
        total += 1
    for child in node.get_children():
        total += _count_runtime_visual_nodes(child)
    return total
