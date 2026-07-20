extends Node2D

@onready var runtime: Node2D = $SasukeAnimationRuntime
@onready var director: SasukeAnimationDirector = $SasukeAnimationRuntime/AnimationDirector
@onready var status_label: Label = $Interface/Panel/Margin/VBox/Status
@onready var low_flash_toggle: CheckButton = $Interface/Panel/Margin/VBox/LowFlash

func _ready() -> void:
    $Interface/Panel/Margin/VBox/Strike.pressed.connect(_play.bind("strike_kusanagi_draw_slash"))
    $Interface/Panel/Margin/VBox/Defend.pressed.connect(_play.bind("defend_wire_parry_stance"))
    $Interface/Panel/Margin/VBox/Bash.pressed.connect(_play.bind("bash_sharingan_breaker"))
    $Interface/Panel/Margin/VBox/Cancel.pressed.connect(_cancel)
    director.timeline_started.connect(_on_started)
    director.impact.connect(_on_impact)
    director.timeline_completed.connect(_on_completed)
    director.timeline_failed.connect(_on_failed)
    status_label.text = "Ready — choose a card timeline"

func _play(animation_id: String) -> void:
    var variant := "low_flash" if low_flash_toggle.button_pressed else "base"
    status_label.text = "Loading %s (%s)…" % [animation_id, variant]
    director.play_timeline(animation_id, variant, {
        "low_flash": low_flash_toggle.button_pressed,
        "quality_scale": 1.0
    })

func _cancel() -> void:
    director.cancel_current()
    status_label.text = "Cancelled and returned to idle"

func _on_started(animation_id: String, variant: String) -> void:
    status_label.text = "Playing %s — %s" % [animation_id, variant]

func _on_impact(animation_id: String, impact_index: int) -> void:
    status_label.text = "%s — impact %d" % [animation_id, impact_index + 1]

func _on_completed(animation_id: String) -> void:
    status_label.text = "%s completed" % animation_id

func _on_failed(animation_id: String, reason: String) -> void:
    status_label.text = "%s failed: %s" % [animation_id, reason]
