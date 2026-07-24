extends SasukeGrayboxRig
class_name SasukeStatefulGrayboxRig

const STATEFUL_POSES := {
    "flame_seal": {
        "root_position": Vector2(-10, 0), "root_rotation": -0.05,
        "torso_rotation": -0.10, "head_rotation": 0.08,
        "left_arm_rotation": -1.08, "right_arm_rotation": -0.82,
        "left_leg_rotation": 0.18, "right_leg_rotation": -0.14,
        "sword_rotation": -0.70, "sword_position": Vector2(16, -24)
    },
    "flame_guard_hold": {
        "root_position": Vector2(-4, 4), "root_rotation": -0.02,
        "torso_rotation": -0.05, "head_rotation": 0.02,
        "left_arm_rotation": -0.44, "right_arm_rotation": -1.18,
        "left_leg_rotation": 0.22, "right_leg_rotation": -0.18,
        "sword_rotation": -1.10, "sword_position": Vector2(14, -38)
    },
    "pact_focus": {
        "root_position": Vector2(-12, -2), "root_rotation": -0.06,
        "torso_rotation": -0.14, "head_rotation": 0.12,
        "left_arm_rotation": -0.88, "right_arm_rotation": -0.58,
        "left_leg_rotation": 0.16, "right_leg_rotation": -0.12,
        "sword_rotation": -0.82, "sword_position": Vector2(14, -30)
    },
    "pact_consume": {
        "root_position": Vector2(12, -4), "root_rotation": 0.08,
        "torso_rotation": 0.18, "head_rotation": -0.08,
        "left_arm_rotation": 0.55, "right_arm_rotation": 0.84,
        "left_leg_rotation": -0.12, "right_leg_rotation": 0.16,
        "sword_rotation": 0.30, "sword_position": Vector2(42, -4)
    },
    "limit_focus": {
        "root_position": Vector2(-6, 0), "root_rotation": -0.03,
        "torso_rotation": -0.08, "head_rotation": -0.02,
        "left_arm_rotation": -0.42, "right_arm_rotation": -0.68,
        "left_leg_rotation": 0.10, "right_leg_rotation": -0.10,
        "sword_rotation": -0.88, "sword_position": Vector2(18, -26)
    },
    "limit_overdrive": {
        "root_position": Vector2(8, -6), "root_rotation": 0.04,
        "torso_rotation": 0.10, "head_rotation": -0.10,
        "left_arm_rotation": -0.18, "right_arm_rotation": -1.20,
        "left_leg_rotation": -0.06, "right_leg_rotation": 0.12,
        "sword_rotation": -1.42, "sword_position": Vector2(20, -58)
    }
}

func set_pose_immediate(pose_name: String) -> void:
    if not STATEFUL_POSES.has(pose_name):
        super.set_pose_immediate(pose_name)
        return
    _apply_pose_values(STATEFUL_POSES[pose_name])

func tween_pose(pose_name: String, duration: float = 0.18) -> void:
    if not STATEFUL_POSES.has(pose_name):
        await super.tween_pose(pose_name, duration)
        return
    if is_instance_valid(_pose_tween):
        _pose_tween.kill()
    var pose: Dictionary = STATEFUL_POSES[pose_name]
    _pose_tween = create_tween().set_parallel(true)
    _pose_tween.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
    _pose_tween.tween_property(visual_root, "position", pose.root_position, duration)
    _pose_tween.tween_property(visual_root, "rotation", pose.root_rotation, duration)
    _pose_tween.tween_property(torso, "rotation", pose.torso_rotation, duration)
    _pose_tween.tween_property(head, "rotation", pose.head_rotation, duration)
    _pose_tween.tween_property(left_arm, "rotation", pose.left_arm_rotation, duration)
    _pose_tween.tween_property(right_arm, "rotation", pose.right_arm_rotation, duration)
    _pose_tween.tween_property(left_leg, "rotation", pose.left_leg_rotation, duration)
    _pose_tween.tween_property(right_leg, "rotation", pose.right_leg_rotation, duration)
    _pose_tween.tween_property(sword, "rotation", pose.sword_rotation, duration)
    _pose_tween.tween_property(sword, "position", pose.sword_position, duration)
    await _pose_tween.finished
    pose_finished.emit(pose_name)
