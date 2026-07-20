extends CanvasLayer
class_name SasukeCameraEffectDirector

@onready var flash_overlay: ColorRect = $FlashOverlay
@onready var dim_overlay: ColorRect = $DimOverlay

var low_flash_mode := false
var shake_target: Node2D
var _base_position := Vector2.ZERO
var _shake_generation := 0

func configure(target: Node2D, low_flash: bool) -> void:
    shake_target = target
    low_flash_mode = low_flash
    if is_instance_valid(target):
        _base_position = target.position

func apply_effect(effect_id: String, params: Dictionary = {}) -> void:
    match effect_id:
        "impact_flash": _flash(float(params.get("opacity", 0.24)), float(params.get("duration", 0.08)))
        "scene_dim": _dim(float(params.get("opacity", 0.22)), float(params.get("duration", 0.22)))
        "shake_light": _shake(float(params.get("amplitude", 3.0)), float(params.get("duration", 0.12)))
        "shake_medium": _shake(float(params.get("amplitude", 7.0)), float(params.get("duration", 0.18)))

func reset_all() -> void:
    _shake_generation += 1
    flash_overlay.modulate.a = 0.0
    dim_overlay.modulate.a = 0.0
    if is_instance_valid(shake_target):
        shake_target.position = _base_position

func _flash(opacity: float, duration: float) -> void:
    if low_flash_mode:
        opacity = minf(opacity, 0.08)
        duration = maxf(duration, 0.12)
    flash_overlay.modulate.a = 0.0
    var tween := create_tween()
    tween.tween_property(flash_overlay, "modulate:a", opacity, duration * 0.35)
    tween.tween_property(flash_overlay, "modulate:a", 0.0, duration * 0.65)

func _dim(opacity: float, duration: float) -> void:
    opacity = minf(opacity, 0.12) if low_flash_mode else opacity
    var tween := create_tween()
    tween.tween_property(dim_overlay, "modulate:a", opacity, duration * 0.35)
    tween.tween_interval(duration * 0.30)
    tween.tween_property(dim_overlay, "modulate:a", 0.0, duration * 0.35)

func _shake(amplitude: float, duration: float) -> void:
    if not is_instance_valid(shake_target):
        return
    amplitude *= 0.25 if low_flash_mode else 1.0
    _shake_generation += 1
    var generation := _shake_generation
    _base_position = shake_target.position
    var steps := maxi(2, int(duration / 0.025))
    for index in range(steps):
        if generation != _shake_generation or not is_instance_valid(shake_target):
            return
        var direction := Vector2(-1.0 if index % 2 == 0 else 1.0, 0.5 if index % 3 == 0 else -0.5)
        shake_target.position = _base_position + direction * amplitude * (1.0 - float(index) / steps)
        await get_tree().create_timer(duration / steps).timeout
    if generation == _shake_generation and is_instance_valid(shake_target):
        shake_target.position = _base_position
