extends CanvasLayer
class_name SasukeCutinDirector

@onready var root: Control = $Root
@onready var shade: ColorRect = $Root/Shade
@onready var band: ColorRect = $Root/Band
@onready var eye: Polygon2D = $Root/Band/Eye
@onready var slash: Line2D = $Root/Band/Slash

var low_flash_mode := false
var _generation := 0
var _enter_tween: Tween
var _exit_tween: Tween

func configure(low_flash: bool) -> void:
    low_flash_mode = low_flash

func play_cutin(style: String, duration_ms: int = 260) -> void:
    clear_all()
    if low_flash_mode:
        return

    _generation += 1
    var generation := _generation
    root.visible = true
    shade.modulate.a = 0.0
    band.modulate.a = 0.0
    band.position.x = -260.0
    eye.modulate.a = 0.0
    slash.modulate.a = 0.0

    match style:
        "sharingan_side":
            band.color = Color(0.055, 0.065, 0.11, 0.96)
            eye.color = Color(0.88, 0.08, 0.13, 0.92)
            slash.default_color = Color(0.45, 0.86, 1.0, 0.85)
        "dragon_fire":
            band.color = Color(0.11, 0.045, 0.025, 0.96)
            eye.color = Color(0.92, 0.12, 0.08, 0.94)
            slash.default_color = Color(1.0, 0.43, 0.12, 0.92)
        "curse_stage_two":
            band.color = Color(0.075, 0.035, 0.11, 0.97)
            eye.color = Color(0.76, 0.10, 0.22, 0.94)
            slash.default_color = Color(0.66, 0.22, 0.88, 0.94)
        _:
            band.color = Color(0.08, 0.09, 0.14, 0.94)
            eye.color = Color(0.80, 0.12, 0.16, 0.88)
            slash.default_color = Color(0.72, 0.84, 1.0, 0.82)

    var duration := maxf(0.12, float(duration_ms) / 1000.0)
    var enter_duration := minf(0.11, duration * 0.32)
    var exit_duration := minf(0.13, duration * 0.36)
    var hold_duration := maxf(0.02, duration - enter_duration - exit_duration)

    _enter_tween = create_tween().set_parallel(true)
    _enter_tween.tween_property(shade, "modulate:a", 0.32, enter_duration)
    _enter_tween.tween_property(band, "modulate:a", 1.0, enter_duration)
    _enter_tween.tween_property(band, "position:x", 0.0, enter_duration).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
    _enter_tween.tween_property(eye, "modulate:a", 1.0, enter_duration * 0.75)
    _enter_tween.tween_property(slash, "modulate:a", 1.0, enter_duration)
    if not await _wait_for_generation(enter_duration, generation):
        return

    if not await _wait_for_generation(hold_duration, generation):
        return

    _exit_tween = create_tween().set_parallel(true)
    _exit_tween.tween_property(shade, "modulate:a", 0.0, exit_duration)
    _exit_tween.tween_property(band, "modulate:a", 0.0, exit_duration)
    _exit_tween.tween_property(band, "position:x", 220.0, exit_duration).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
    _exit_tween.tween_property(eye, "modulate:a", 0.0, exit_duration)
    _exit_tween.tween_property(slash, "modulate:a", 0.0, exit_duration)
    if not await _wait_for_generation(exit_duration, generation):
        return

    if generation == _generation:
        root.visible = false
        _enter_tween = null
        _exit_tween = null

func clear_all() -> void:
    _generation += 1
    _kill_tween(_enter_tween)
    _kill_tween(_exit_tween)
    _enter_tween = null
    _exit_tween = null
    if is_instance_valid(root):
        root.visible = false
    if is_instance_valid(shade):
        shade.modulate.a = 0.0
    if is_instance_valid(band):
        band.modulate.a = 0.0
        band.position.x = 0.0
    if is_instance_valid(eye):
        eye.modulate.a = 0.0
    if is_instance_valid(slash):
        slash.modulate.a = 0.0

func _wait_for_generation(seconds: float, generation: int) -> bool:
    var deadline_ms := Time.get_ticks_msec() + int(maxf(0.01, seconds) * 1000.0)
    while generation == _generation:
        if Time.get_ticks_msec() >= deadline_ms:
            return true
        await get_tree().process_frame
    return false

func _kill_tween(tween: Tween) -> void:
    if tween != null and tween.is_valid():
        tween.kill()
