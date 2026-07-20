extends Node

const ITERATIONS := 100
const TIMELINES := [
    "strike_kusanagi_draw_slash",
    "defend_wire_parry_guard",
    "bash_sharingan_breaker",
    "anger_shuriken_afterimage",
    "cleave_chidori_ground_arc",
    "thunderclap_chidori_ring_burst",
    "heavy_blade_lightning_execution",
    "whirlwind_chidori_blade_storm",
    "fiend_fire_dragon_flame_annihilation"
]

@onready var runtime: Node2D = $SasukeAnimationRuntime
@onready var director: SasukeAnimationDirector = $SasukeAnimationRuntime/AnimationDirector
@onready var cutin_root: Control = $SasukeAnimationRuntime/CutinDirector/Root

var failures: Array[String] = []

func _ready() -> void:
    await get_tree().process_frame
    var baseline_nodes := _count_nodes(runtime)
    var peak_nodes := baseline_nodes

    for iteration in range(ITERATIONS):
        var animation_id := String(TIMELINES[iteration % TIMELINES.size()])
        var hit_count := 4 if animation_id in ["whirlwind_chidori_blade_storm", "fiend_fire_dragon_flame_annihilation"] else 1
        var context := {
            "low_flash": iteration % 5 == 0,
            "quality_scale": 0.5,
            "strength": 8,
            "target_count": 3,
            "energy_spent": 4,
            "exhausted_card_count": 4,
            "hit_count": hit_count
        }

        if iteration % 3 == 0:
            director.play_timeline(animation_id, "fast", context)
            await get_tree().create_timer(0.035).timeout
            director.cancel_current()
        else:
            var completed := await director.play_timeline(animation_id, "fast", context)
            if not completed:
                failures.append("timeline did not complete: %s at iteration %d" % [animation_id, iteration])

        await get_tree().process_frame
        await get_tree().process_frame
        await get_tree().create_timer(0.60).timeout
        await get_tree().process_frame

        var transient_nodes := _count_transient_nodes(runtime)
        var current_nodes := _count_nodes(runtime)
        peak_nodes = maxi(peak_nodes, current_nodes)

        if transient_nodes != 0:
            failures.append("transient nodes remained after iteration %d: %d" % [iteration, transient_nodes])
        if cutin_root.visible:
            failures.append("Cut-in remained visible after iteration %d" % iteration)
        if current_nodes > baseline_nodes + 2:
            failures.append("runtime node count grew from %d to %d at iteration %d" % [baseline_nodes, current_nodes, iteration])

    director.cancel_current()
    await get_tree().process_frame
    await get_tree().process_frame

    if failures.is_empty():
        print("STRESS_OK iterations=%d baseline_nodes=%d peak_nodes=%d" % [ITERATIONS, baseline_nodes, peak_nodes])
        get_tree().quit(0)
        return

    for failure in failures:
        push_error(failure)
    push_error("STRESS_FAILED failures=%d baseline_nodes=%d peak_nodes=%d" % [failures.size(), baseline_nodes, peak_nodes])
    get_tree().quit(1)

func _count_nodes(node: Node) -> int:
    var total := 1
    for child in node.get_children():
        total += _count_nodes(child)
    return total

func _count_transient_nodes(node: Node) -> int:
    var total := 0
    var node_name := String(node.name)
    if node_name.begins_with("VFX_") or node_name.begins_with("AdvancedVFX_"):
        total += 1
    for child in node.get_children():
        total += _count_transient_nodes(child)
    return total