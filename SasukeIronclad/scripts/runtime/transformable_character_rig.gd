extends SasukeGrayboxRig
class_name SasukeTransformableRig

@onready var curse_mark_overlay: Line2D = $VisualRoot/CurseMarkOverlay
@onready var wing_left: Line2D = $VisualRoot/WingLeft
@onready var wing_right: Line2D = $VisualRoot/WingRight

var _active_form_id := ""
var _form_params: Dictionary = {}

const FORM_POSES := {
    "demon_transform_start": {
        "root_position": Vector2(-10, 8),
        "root_rotation": -0.06,
        "torso_rotation": -0.18,
        "head_rotation": -0.20,
        "left_arm_rotation": -0.42,
        "right_arm_rotation": -0.72,
        "left_leg_rotation": 0.30,
        "right_leg_rotation": -0.24,
        "sword_rotation": -0.92,
        "sword_position": Vector2(16, -30)
    },
    "demon_transform_reveal": {
        "root_position": Vector2(4, -10),
        "root_rotation": 0.04,
        "torso_rotation": 0.08,
        "head_rotation": -0.04,
        "left_arm_rotation": -1.02,
        "right_arm_rotation": 0.84,
        "left_leg_rotation": -0.04,
        "right_leg_rotation": 0.08,
        "sword_rotation": 0.54,
        "sword_position": Vector2(54, -34)
    },
    "demon_idle": {
        "root_position": Vector2(-2, -4),
        "root_rotation": -0.02,
        "torso_rotation": -0.04,
        "head_rotation": 0.08,
        "left_arm_rotation": -0.56,
        "right_arm_rotation": -0.86,
        "left_leg_rotation": 0.22,
        "right_leg_rotation": -0.16,
        "sword_rotation": -0.96,
        "sword_position": Vector2(20, -34)
    }
}

func _ready() -> void:
    super._ready()
    _apply_form_visibility(false)

func apply_visual_form(form_id: String, params: Dictionary = {}) -> void:
    _active_form_id = form_id
    _form_params = params.duplicate(true)
    match form_id:
        "curse_mark_stage_two":
            _apply_form_visibility(true)
            var intensity := clampf(float(params.get("intensity", 1.0)), 0.5, 1.4)
            curse_mark_overlay.width = 3.2 * intensity
            wing_left.width = 11.0 * intensity
            wing_right.width = 11.0 * intensity
            visual_root.modulate = Color(0.84, 0.78, 0.94, 1.0)
        _:
            clear_visual_form()

func clear_visual_form(form_id: String = "") -> void:
    if not form_id.is_empty() and form_id != _active_form_id:
        return
    _active_form_id = ""
    _form_params.clear()
    _apply_form_visibility(false)
    visual_root.modulate = Color.WHITE

func active_visual_form_id() -> String:
    return _active_form_id

func set_pose_immediate(pose_name: String) -> void:
    if FORM_POSES.has(pose_name):
        _apply_pose_values(FORM_POSES[pose_name])
        return
    super.set_pose_immediate(pose_name)

func tween_pose(pose_name: String, duration: float = 0.18) -> void:
    if not FORM_POSES.has(pose_name):
        await super.tween_pose(pose_name, duration)
        return
    if is_instance_valid(_pose_tween):
        _pose_tween.kill()
    var pose: Dictionary = FORM_POSES[pose_name]
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

func reset_to_idle(duration: float = 0.16) -> void:
    set_eye_active(false)
    var idle_pose := "demon_idle" if _active_form_id == "curse_mark_stage_two" else "idle_sword_ready"
    await tween_pose(idle_pose, duration)

func _apply_form_visibility(enabled: bool) -> void:
    curse_mark_overlay.visible = enabled
    wing_left.visible = enabled
    wing_right.visible = enabled
    if enabled:
        curse_mark_overlay.default_color = Color(0.48, 0.16, 0.68, 0.76 if low_flash_mode else 0.94)
        wing_left.default_color = Color(0.22, 0.12, 0.32, 0.66 if low_flash_mode else 0.90)
        wing_right.default_color = wing_left.default_color
