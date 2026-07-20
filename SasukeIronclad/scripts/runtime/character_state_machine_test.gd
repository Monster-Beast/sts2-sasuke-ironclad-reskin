extends Node

@onready var runtime: Node2D = $SasukeAnimationRuntime
@onready var director: SasukeStatefulAnimationDirector = $SasukeAnimationRuntime/AnimationDirector
@onready var rig: SasukeCharacterStateRig = $SasukeAnimationRuntime/CharacterRig

var failures: Array[String] = []

func _ready() -> void:
    await get_tree().process_frame
    director.reset_character_state_machine()

    _expect(await director.play_character_state("combat_entry", {"force": true}), "combat entry failed")
    _expect(director.current_character_state_id() == "idle_sword_ready", "entry did not return to base idle")

    _expect(await director.play_character_state("hit_light", {"intensity": 0.8}), "base light hit failed")
    _expect(director.current_character_state_id() == "idle_sword_ready", "base light hit did not recover")

    _expect(await director.play_character_state("hit_heavy", {"intensity": 1.3}), "base heavy hit failed")
    _expect(director.current_character_state_id() == "idle_sword_ready", "base heavy hit did not recover")

    var demon_ok := await director.play_timeline("demon_form_curse_mark_stage_two", "base", {
        "low_flash": true,
        "fast_mode": true,
        "quality_scale": 0.5,
        "hit_count": 1,
        "strength": 8
    })
    _expect(demon_ok, "Demon Form setup failed")
    _expect(rig.active_visual_form_id() == "curse_mark_stage_two", "Demon Form was not committed")

    _expect(await director.play_character_state("hit_light", {"intensity": 0.9}), "demon light hit failed")
    _expect(director.current_character_state_id() == "demon_idle", "demon light hit did not recover to demon idle")

    _expect(await director.play_character_state("hit_heavy", {"intensity": 1.4}), "demon heavy hit failed")
    _expect(director.current_character_state_id() == "demon_idle", "demon heavy hit did not recover to demon idle")

    _expect(await director.play_character_state("victory", {"force": true}), "demon victory failed")
    _expect(director.is_character_terminal(), "victory did not lock terminal state")
    _expect(director.current_character_state_id() == "victory", "victory state id was not retained")
    var rejected_after_victory := await director.play_character_state("hit_light")
    _expect(not rejected_after_victory, "lower-priority hit was accepted after victory")

    # Death is the only non-forced request allowed to override a victory terminal.
    _expect(await director.play_character_state("death"), "death did not override victory without force")
    _expect(director.is_character_terminal(), "death did not retain terminal state")
    _expect(director.current_character_state_id() == "death", "death state id was not retained")
    var rejected_after_death := await director.play_character_state("victory")
    _expect(not rejected_after_death, "victory was accepted after death")

    director.release_combat_resources()
    await get_tree().process_frame
    await get_tree().process_frame

    _expect(not director.is_character_terminal(), "combat release left terminal state active")
    _expect(director.current_character_state_id().is_empty(), "combat release left character state active")
    _expect(rig.active_visual_form_id().is_empty(), "combat release left Demon Form active")
    _expect(director.active_visual_state_count() == 0, "combat release left persistent visual states")
    _expect(_count_transients(runtime) == 0, "combat release left transient VFX")

    if failures.is_empty():
        print("CHAR_STATE_OK state='%s' terminal=%s form='%s'" % [
            director.current_character_state_id(),
            str(director.is_character_terminal()),
            rig.active_visual_form_id()
        ])
        get_tree().quit(0)
        return

    for failure in failures:
        push_error(failure)
    push_error("CHAR_STATE_FAILED failures=%d" % failures.size())
    get_tree().quit(1)

func _expect(condition: bool, message: String) -> void:
    if not condition:
        failures.append(message)

func _count_transients(node: Node) -> int:
    var total := 0
    var node_name := String(node.name)
    if node_name.begins_with("VFX_") or node_name.begins_with("AdvancedVFX_"):
        total += 1
    for child in node.get_children():
        total += _count_transients(child)
    return total
