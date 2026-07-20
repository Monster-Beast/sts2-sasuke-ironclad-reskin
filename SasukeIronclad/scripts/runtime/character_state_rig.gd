extends SasukeTransformableRig
class_name SasukeCharacterStateRig

const CHARACTER_STATE_POSES := {
    "combat_entry_start": {
        "root_position": Vector2(-26, 2), "root_rotation": -0.10,
        "torso_rotation": -0.20, "head_rotation": 0.12,
        "left_arm_rotation": 0.44, "right_arm_rotation": -1.02,
        "left_leg_rotation": 0.36, "right_leg_rotation": -0.28,
        "sword_rotation": -1.18, "sword_position": Vector2(8, -32)
    },
    "combat_entry_land": {
        "root_position": Vector2(10, 8), "root_rotation": 0.08,
        "torso_rotation": 0.18, "head_rotation": -0.08,
        "left_arm_rotation": 0.24, "right_arm_rotation": -0.52,
        "left_leg_rotation": -0.18, "right_leg_rotation": 0.30,
        "sword_rotation": -0.66, "sword_position": Vector2(30, -16)
    },
    "idle_sharingan_alert": {
        "root_position": Vector2(-6, -2), "root_rotation": -0.04,
        "torso_rotation": -0.10, "head_rotation": 0.16,
        "left_arm_rotation": -0.42, "right_arm_rotation": -0.84,
        "left_leg_rotation": 0.18, "right_leg_rotation": -0.14,
        "sword_rotation": -1.02, "sword_position": Vector2(18, -38)
    },
    "demon_idle_sharingan_alert": {
        "root_position": Vector2(-5, -7), "root_rotation": -0.03,
        "torso_rotation": -0.08, "head_rotation": 0.20,
        "left_arm_rotation": -0.64, "right_arm_rotation": -1.02,
        "left_leg_rotation": 0.24, "right_leg_rotation": -0.18,
        "sword_rotation": -1.10, "sword_position": Vector2(18, -44)
    },
    "hit_light": {
        "root_position": Vector2(-18, 1), "root_rotation": -0.12,
        "torso_rotation": -0.24, "head_rotation": 0.14,
        "left_arm_rotation": 0.20, "right_arm_rotation": -0.34,
        "left_leg_rotation": 0.12, "right_leg_rotation": -0.22,
        "sword_rotation": -0.40, "sword_position": Vector2(22, -8)
    },
    "hit_heavy": {
        "root_position": Vector2(-34, 10), "root_rotation": -0.24,
        "torso_rotation": -0.42, "head_rotation": 0.28,
        "left_arm_rotation": 0.58, "right_arm_rotation": 0.18,
        "left_leg_rotation": 0.30, "right_leg_rotation": -0.38,
        "sword_rotation": 0.12, "sword_position": Vector2(38, 18)
    },
    "demon_hit_light": {
        "root_position": Vector2(-15, -3), "root_rotation": -0.10,
        "torso_rotation": -0.20, "head_rotation": 0.12,
        "left_arm_rotation": 0.08, "right_arm_rotation": -0.46,
        "left_leg_rotation": 0.16, "right_leg_rotation": -0.20,
        "sword_rotation": -0.54, "sword_position": Vector2(24, -16)
    },
    "demon_hit_heavy": {
        "root_position": Vector2(-30, 5), "root_rotation": -0.20,
        "torso_rotation": -0.36, "head_rotation": 0.22,
        "left_arm_rotation": 0.44, "right_arm_rotation": 0.06,
        "left_leg_rotation": 0.28, "right_leg_rotation": -0.34,
        "sword_rotation": 0.02, "sword_position": Vector2(42, 10)
    },
    "death_ready": {
        "root_position": Vector2(-12, 12), "root_rotation": -0.12,
        "torso_rotation": -0.22, "head_rotation": -0.20,
        "left_arm_rotation": 0.38, "right_arm_rotation": 0.16,
        "left_leg_rotation": 0.46, "right_leg_rotation": -0.34,
        "sword_rotation": 0.36, "sword_position": Vector2(44, 20)
    },
    "death": {
        "root_position": Vector2(-40, 66), "root_rotation": -1.10,
        "torso_rotation": -0.34, "head_rotation": 0.26,
        "left_arm_rotation": 0.92, "right_arm_rotation": 0.70,
        "left_leg_rotation": 0.82, "right_leg_rotation": -0.66,
        "sword_rotation": 1.24, "sword_position": Vector2(62, 36)
    },
    "demon_death_ready": {
        "root_position": Vector2(-8, 8), "root_rotation": -0.10,
        "torso_rotation": -0.18, "head_rotation": -0.24,
        "left_arm_rotation": 0.28, "right_arm_rotation": 0.02,
        "left_leg_rotation": 0.40, "right_leg_rotation": -0.30,
        "sword_rotation": 0.18, "sword_position": Vector2(46, 12)
    },
    "demon_death": {
        "root_position": Vector2(-34, 58), "root_rotation": -0.94,
        "torso_rotation": -0.30, "head_rotation": 0.22,
        "left_arm_rotation": 0.80, "right_arm_rotation": 0.62,
        "left_leg_rotation": 0.72, "right_leg_rotation": -0.58,
        "sword_rotation": 1.08, "sword_position": Vector2(66, 30)
    },
    "victory_sheathe": {
        "root_position": Vector2(4, -2), "root_rotation": 0.02,
        "torso_rotation": 0.04, "head_rotation": -0.08,
        "left_arm_rotation": -0.52, "right_arm_rotation": -1.24,
        "left_leg_rotation": -0.04, "right_leg_rotation": 0.08,
        "sword_rotation": -1.62, "sword_position": Vector2(-4, -54)
    },
    "demon_victory_sheathe": {
        "root_position": Vector2(6, -7), "root_rotation": 0.03,
        "torso_rotation": 0.08, "head_rotation": -0.10,
        "left_arm_rotation": -0.66, "right_arm_rotation": -1.34,
        "left_leg_rotation": -0.02, "right_leg_rotation": 0.10,
        "sword_rotation": -1.70, "sword_position": Vector2(-2, -62)
    }
}

var _character_pose_generation := 0

func resolve_character_state_pose(state_id: String) -> String:
    var demon := active_visual_form_id() == "curse_mark_stage_two"
    match state_id:
        "idle_neutral": return "demon_idle" if demon else "idle_neutral"
        "idle_sword_ready": return "demon_idle" if demon else "idle_sword_ready"
        "idle_sharingan_alert": return "demon_idle_sharingan_alert" if demon else "idle_sharingan_alert"
        "hit_light": return "demon_hit_light" if demon else "hit_light"
        "hit_heavy": return "demon_hit_heavy" if demon else "hit_heavy"
        "death_ready": return "demon_death_ready" if demon else "death_ready"
        "death": return "demon_death" if demon else "death"
        "victory_sheathe": return "demon_victory_sheathe" if demon else "victory_sheathe"
        _: return state_id

func set_pose_immediate(pose_name: String) -> void:
    var pose := _find_character_pose(pose_name)
    if pose.is_empty():
        push_warning("Unknown graybox pose: %s" % pose_name)
        return
    _cancel_character_pose_tween()
    _apply_pose_values(pose)

func tween_pose(pose_name: String, duration: float = 0.18) -> void:
    var pose := _find_character_pose(pose_name)
    if pose.is_empty():
        push_warning("Unknown graybox pose: %s" % pose_name)
        return

    _cancel_character_pose_tween()
    var generation := _character_pose_generation
    var bounded_duration := maxf(0.01, duration)
    _pose_tween = create_tween().set_parallel(true)
    _pose_tween.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
    _pose_tween.tween_property(visual_root, "position", pose.root_position, bounded_duration)
    _pose_tween.tween_property(visual_root, "rotation", pose.root_rotation, bounded_duration)
    _pose_tween.tween_property(torso, "rotation", pose.torso_rotation, bounded_duration)
    _pose_tween.tween_property(head, "rotation", pose.head_rotation, bounded_duration)
    _pose_tween.tween_property(left_arm, "rotation", pose.left_arm_rotation, bounded_duration)
    _pose_tween.tween_property(right_arm, "rotation", pose.right_arm_rotation, bounded_duration)
    _pose_tween.tween_property(left_leg, "rotation", pose.left_leg_rotation, bounded_duration)
    _pose_tween.tween_property(right_leg, "rotation", pose.right_leg_rotation, bounded_duration)
    _pose_tween.tween_property(sword, "rotation", pose.sword_rotation, bounded_duration)
    _pose_tween.tween_property(sword, "position", pose.sword_position, bounded_duration)

    # A killed Tween does not reliably emit finished. A bounded timer guarantees
    # that superseded coroutines resume and observe the changed generation.
    await get_tree().create_timer(bounded_duration).timeout
    if generation != _character_pose_generation:
        return
    _apply_pose_values(pose)
    _pose_tween = null
    pose_finished.emit(pose_name)

func _find_character_pose(pose_name: String) -> Dictionary:
    if CHARACTER_STATE_POSES.has(pose_name):
        return CHARACTER_STATE_POSES[pose_name]
    if FORM_POSES.has(pose_name):
        return FORM_POSES[pose_name]
    if STATEFUL_POSES.has(pose_name):
        return STATEFUL_POSES[pose_name]
    if POSES.has(pose_name):
        return POSES[pose_name]
    return {}

func _cancel_character_pose_tween() -> void:
    _character_pose_generation += 1
    if is_instance_valid(_pose_tween):
        _pose_tween.kill()
    _pose_tween = null
