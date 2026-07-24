extends Node2D
class_name SasukeGrayboxRig

signal pose_finished(pose_name: String)

@onready var visual_root: Node2D = $VisualRoot
@onready var torso: Polygon2D = $VisualRoot/Torso
@onready var head: Polygon2D = $VisualRoot/Head
@onready var left_arm: Line2D = $VisualRoot/LeftArm
@onready var right_arm: Line2D = $VisualRoot/RightArm
@onready var left_leg: Line2D = $VisualRoot/LeftLeg
@onready var right_leg: Line2D = $VisualRoot/RightLeg
@onready var sword: Line2D = $VisualRoot/Sword
@onready var eye_glow: Polygon2D = $VisualRoot/EyeGlow

@onready var weapon_anchor: Node2D = $WeaponAnchor
@onready var left_hand_anchor: Node2D = $LeftHandAnchor
@onready var right_hand_anchor: Node2D = $RightHandAnchor
@onready var eye_anchor: Node2D = $EyeAnchor
@onready var ground_anchor: Node2D = $GroundAnchor
@onready var vfx_anchor: Node2D = $VfxAnchor

var low_flash_mode := false
var _pose_tween: Tween

const POSES := {
    "idle_neutral": {
        "root_position": Vector2.ZERO,
        "root_rotation": 0.0,
        "torso_rotation": 0.0,
        "head_rotation": 0.0,
        "left_arm_rotation": 0.18,
        "right_arm_rotation": -0.12,
        "left_leg_rotation": 0.05,
        "right_leg_rotation": -0.05,
        "sword_rotation": -0.22,
        "sword_position": Vector2(30, -5)
    },
    "idle_sword_ready": {
        "root_position": Vector2(-4, 1),
        "root_rotation": -0.03,
        "torso_rotation": -0.06,
        "head_rotation": 0.04,
        "left_arm_rotation": -0.28,
        "right_arm_rotation": -0.62,
        "left_leg_rotation": 0.16,
        "right_leg_rotation": -0.12,
        "sword_rotation": -0.72,
        "sword_position": Vector2(22, -18)
    },
    "guard_parry": {
        "root_position": Vector2(-8, 0),
        "root_rotation": -0.08,
        "torso_rotation": -0.12,
        "head_rotation": 0.10,
        "left_arm_rotation": -0.72,
        "right_arm_rotation": -1.08,
        "left_leg_rotation": 0.22,
        "right_leg_rotation": -0.18,
        "sword_rotation": -1.25,
        "sword_position": Vector2(14, -32)
    },
    "draw_windup": {
        "root_position": Vector2(-14, 2),
        "root_rotation": -0.10,
        "torso_rotation": -0.16,
        "head_rotation": 0.10,
        "left_arm_rotation": 0.38,
        "right_arm_rotation": -0.88,
        "left_leg_rotation": 0.20,
        "right_leg_rotation": -0.16,
        "sword_rotation": -0.42,
        "sword_position": Vector2(10, -12)
    },
    "slash_impact": {
        "root_position": Vector2(34, -2),
        "root_rotation": 0.08,
        "torso_rotation": 0.20,
        "head_rotation": -0.06,
        "left_arm_rotation": 0.92,
        "right_arm_rotation": 0.48,
        "left_leg_rotation": -0.16,
        "right_leg_rotation": 0.20,
        "sword_rotation": 0.48,
        "sword_position": Vector2(52, -18)
    },
    "bash_lock": {
        "root_position": Vector2(-10, 0),
        "root_rotation": -0.05,
        "torso_rotation": -0.08,
        "head_rotation": 0.16,
        "left_arm_rotation": -0.30,
        "right_arm_rotation": -0.56,
        "left_leg_rotation": 0.14,
        "right_leg_rotation": -0.10,
        "sword_rotation": -0.66,
        "sword_position": Vector2(20, -20)
    },
    "bash_impact": {
        "root_position": Vector2(46, -1),
        "root_rotation": 0.12,
        "torso_rotation": 0.22,
        "head_rotation": -0.12,
        "left_arm_rotation": 0.46,
        "right_arm_rotation": 0.72,
        "left_leg_rotation": -0.18,
        "right_leg_rotation": 0.22,
        "sword_rotation": 0.12,
        "sword_position": Vector2(46, -4)
    },
    "cleave_windup": {
        "root_position": Vector2(-20, 8),
        "root_rotation": -0.14,
        "torso_rotation": -0.22,
        "head_rotation": 0.12,
        "left_arm_rotation": 0.46,
        "right_arm_rotation": -1.02,
        "left_leg_rotation": 0.34,
        "right_leg_rotation": -0.26,
        "sword_rotation": -1.46,
        "sword_position": Vector2(6, 10)
    },
    "cleave_impact": {
        "root_position": Vector2(30, 10),
        "root_rotation": 0.06,
        "torso_rotation": 0.18,
        "head_rotation": -0.04,
        "left_arm_rotation": 0.78,
        "right_arm_rotation": 0.28,
        "left_leg_rotation": -0.20,
        "right_leg_rotation": 0.28,
        "sword_rotation": 0.04,
        "sword_position": Vector2(66, 22)
    },
    "heavy_charge": {
        "root_position": Vector2(-18, 4),
        "root_rotation": -0.08,
        "torso_rotation": -0.18,
        "head_rotation": 0.14,
        "left_arm_rotation": -0.52,
        "right_arm_rotation": -0.96,
        "left_leg_rotation": 0.26,
        "right_leg_rotation": -0.22,
        "sword_rotation": -1.18,
        "sword_position": Vector2(10, -36)
    },
    "heavy_overhead": {
        "root_position": Vector2(4, -4),
        "root_rotation": 0.02,
        "torso_rotation": -0.02,
        "head_rotation": -0.08,
        "left_arm_rotation": -1.22,
        "right_arm_rotation": -1.42,
        "left_leg_rotation": 0.08,
        "right_leg_rotation": -0.08,
        "sword_rotation": -1.72,
        "sword_position": Vector2(16, -88)
    },
    "heavy_impact": {
        "root_position": Vector2(48, 8),
        "root_rotation": 0.18,
        "torso_rotation": 0.30,
        "head_rotation": -0.16,
        "left_arm_rotation": 1.10,
        "right_arm_rotation": 0.92,
        "left_leg_rotation": -0.24,
        "right_leg_rotation": 0.32,
        "sword_rotation": 1.20,
        "sword_position": Vector2(58, 38)
    },
    "heavy_recover": {
        "root_position": Vector2(32, 7),
        "root_rotation": 0.10,
        "torso_rotation": 0.16,
        "head_rotation": -0.04,
        "left_arm_rotation": 0.42,
        "right_arm_rotation": 0.34,
        "left_leg_rotation": -0.12,
        "right_leg_rotation": 0.18,
        "sword_rotation": 0.84,
        "sword_position": Vector2(48, 20)
    },
    "anger_throw_windup": {
        "root_position": Vector2(-16, -2),
        "root_rotation": -0.09,
        "torso_rotation": -0.16,
        "head_rotation": 0.08,
        "left_arm_rotation": 0.52,
        "right_arm_rotation": -1.35,
        "left_leg_rotation": 0.24,
        "right_leg_rotation": -0.18,
        "sword_rotation": -0.92,
        "sword_position": Vector2(12, -34)
    },
    "anger_throw_release": {
        "root_position": Vector2(20, -4),
        "root_rotation": 0.07,
        "torso_rotation": 0.18,
        "head_rotation": -0.10,
        "left_arm_rotation": -0.15,
        "right_arm_rotation": 0.92,
        "left_leg_rotation": -0.14,
        "right_leg_rotation": 0.22,
        "sword_rotation": -0.48,
        "sword_position": Vector2(24, -18)
    },
    "thunder_charge": {
        "root_position": Vector2(-12, 8),
        "root_rotation": -0.08,
        "torso_rotation": -0.20,
        "head_rotation": 0.12,
        "left_arm_rotation": -0.80,
        "right_arm_rotation": -1.10,
        "left_leg_rotation": 0.30,
        "right_leg_rotation": -0.24,
        "sword_rotation": -0.54,
        "sword_position": Vector2(18, -20)
    },
    "thunder_slam": {
        "root_position": Vector2(18, 12),
        "root_rotation": 0.15,
        "torso_rotation": 0.28,
        "head_rotation": -0.12,
        "left_arm_rotation": 1.15,
        "right_arm_rotation": 0.95,
        "left_leg_rotation": -0.20,
        "right_leg_rotation": 0.28,
        "sword_rotation": 0.20,
        "sword_position": Vector2(40, 12)
    }
}

func _ready() -> void:
    set_pose_immediate("idle_neutral")
    set_eye_active(false)

func set_low_flash(enabled: bool) -> void:
    low_flash_mode = enabled
    if enabled:
        eye_glow.modulate.a = minf(eye_glow.modulate.a, 0.35)

func get_anchor(anchor_name: String) -> Node2D:
    match anchor_name:
        "weapon": return weapon_anchor
        "left_hand": return left_hand_anchor
        "right_hand": return right_hand_anchor
        "eye": return eye_anchor
        "ground": return ground_anchor
        _: return vfx_anchor

func set_eye_active(active: bool) -> void:
    eye_glow.visible = active
    eye_glow.modulate.a = 0.25 if low_flash_mode else 0.85

func set_pose_immediate(pose_name: String) -> void:
    if not POSES.has(pose_name):
        push_warning("Unknown graybox pose: %s" % pose_name)
        return
    _apply_pose_values(POSES[pose_name])

func tween_pose(pose_name: String, duration: float = 0.18) -> void:
    if not POSES.has(pose_name):
        push_warning("Unknown graybox pose: %s" % pose_name)
        return
    if is_instance_valid(_pose_tween):
        _pose_tween.kill()
    var pose: Dictionary = POSES[pose_name]
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
    await tween_pose("idle_sword_ready", duration)

func _apply_pose_values(pose: Dictionary) -> void:
    visual_root.position = pose.root_position
    visual_root.rotation = pose.root_rotation
    torso.rotation = pose.torso_rotation
    head.rotation = pose.head_rotation
    left_arm.rotation = pose.left_arm_rotation
    right_arm.rotation = pose.right_arm_rotation
    left_leg.rotation = pose.left_leg_rotation
    right_leg.rotation = pose.right_leg_rotation
    sword.rotation = pose.sword_rotation
    sword.position = pose.sword_position