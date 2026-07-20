extends Node

@onready var runtime: Node2D = $SasukeAnimationRuntime
@onready var director: SasukeStatefulAnimationDirector = $SasukeAnimationRuntime/AnimationDirector

var failures: Array[String] = []

func _ready() -> void:
    await get_tree().process_frame
    var context := {
        "low_flash": false,
        "fast_mode": true,
        "quality_scale": 0.5,
        "strength": 6,
        "target_count": 1,
        "energy_spent": 1,
        "exhausted_card_count": 1,
        "hit_count": 1
    }

    # Cancel after both form_install and state_install have executed. This proves
    # rollback, rather than merely cancelling before any persistent state exists.
    director.play_timeline("demon_form_curse_mark_stage_two", "base", context)
    await get_tree().create_timer(0.66).timeout
    _expect(director.active_visual_form_id() == "curse_mark_stage_two", "test did not reach pending Demon Form installation")
    _expect(director.has_visual_state("demon_form_stage_two_aura"), "test did not reach pending Demon Form aura installation")
    director.cancel_current()
    await _settle()
    _expect(director.active_visual_form_id().is_empty(), "cancelled transformation retained a visual form")
    _expect(not director.has_visual_state("demon_form_stage_two_aura"), "cancelled transformation retained its aura")

    var transformed := await director.play_timeline("demon_form_curse_mark_stage_two", "base", context)
    _expect(transformed, "Demon Form timeline did not complete")
    _expect(director.active_visual_form_id() == "curse_mark_stage_two", "Demon Form was not committed")
    _expect(director.has_visual_state("demon_form_stage_two_aura"), "Demon Form aura was not committed")

    # Reinstall the already committed Form and cancel after the pending replacement
    # appears. Rollback must restore the previous committed Form and aura.
    director.play_timeline("demon_form_curse_mark_stage_two", "base", context)
    await get_tree().create_timer(0.66).timeout
    director.cancel_current()
    await _settle()
    _expect(director.active_visual_form_id() == "curse_mark_stage_two", "cancelled replacement removed committed Demon Form")
    _expect(director.has_visual_state("demon_form_stage_two_aura"), "cancelled replacement removed committed Demon Form aura")

    var strike_completed := await director.play_timeline("strike_kusanagi_draw_slash", "base", context)
    _expect(strike_completed, "Strike did not complete after Demon Form")
    _expect(director.active_visual_form_id() == "curse_mark_stage_two", "later card cleared Demon Form")

    director.clear_visual_form("curse_mark_stage_two")
    await _settle()
    _expect(director.active_visual_form_id().is_empty(), "explicit form removal failed")
    _expect(not director.has_visual_state("demon_form_stage_two_aura"), "explicit form removal left the aura")

    transformed = await director.play_timeline("demon_form_curse_mark_stage_two", "base", context)
    _expect(transformed, "second Demon Form timeline did not complete")
    director.release_combat_resources()
    await _settle()
    _expect(director.active_visual_form_id().is_empty(), "combat release retained Demon Form")
    _expect(director.active_visual_state_count() == 0, "combat release retained visual states")
    _expect(_count_transient_nodes(runtime) == 0, "combat release retained transient VFX")

    if failures.is_empty():
        print("FORM_OK form='' states=0 transients=0")
        get_tree().quit(0)
        return

    for failure in failures:
        push_error(failure)
    push_error("FORM_FAILED failures=%d" % failures.size())
    get_tree().quit(1)

func _settle() -> void:
    await get_tree().process_frame
    await get_tree().process_frame
    await get_tree().create_timer(0.12).timeout
    await get_tree().process_frame

func _expect(condition: bool, message: String) -> void:
    if not condition:
        failures.append(message)

func _count_transient_nodes(node: Node) -> int:
    var total := 0
    var node_name := String(node.name)
    if node_name.begins_with("VFX_") or node_name.begins_with("AdvancedVFX_"):
        total += 1
    for child in node.get_children():
        total += _count_transient_nodes(child)
    return total
