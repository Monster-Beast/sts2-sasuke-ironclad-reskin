extends Node2D

@onready var runtime: Node2D = $SasukeAnimationRuntime
@onready var director: SasukeStatefulAnimationDirector = $SasukeAnimationRuntime/AnimationDirector
@onready var status_label: Label = $Interface/Panel/Margin/VBox/Status
@onready var low_flash_toggle: CheckButton = $Interface/Panel/Margin/VBox/LowFlash
@onready var fast_toggle: CheckButton = $Interface/Panel/Margin/VBox/FastMode
@onready var empowered_toggle: CheckButton = $Interface/Panel/Margin/VBox/Empowered

func _ready() -> void:
    $Interface/Panel/Margin/VBox/Strike.pressed.connect(_play.bind("strike_kusanagi_draw_slash"))
    $Interface/Panel/Margin/VBox/Defend.pressed.connect(_play.bind("defend_wire_parry_guard"))
    $Interface/Panel/Margin/VBox/Bash.pressed.connect(_play.bind("bash_sharingan_breaker"))
    $Interface/Panel/Margin/VBox/Anger.pressed.connect(_play.bind("anger_shuriken_afterimage"))
    $Interface/Panel/Margin/VBox/Cleave.pressed.connect(_play.bind("cleave_chidori_ground_arc"))
    $Interface/Panel/Margin/VBox/Thunderclap.pressed.connect(_play.bind("thunderclap_chidori_ring_burst"))
    $Interface/Panel/Margin/VBox/HeavyBlade.pressed.connect(_play.bind("heavy_blade_lightning_execution"))
    $Interface/Panel/Margin/VBox/FlameBarrier.pressed.connect(_play.bind("flame_barrier_uchiha_fire_guard"))
    $Interface/Panel/Margin/VBox/Whirlwind.pressed.connect(_play.bind("whirlwind_chidori_blade_storm"))
    $Interface/Panel/Margin/VBox/BurningPact.pressed.connect(_play.bind("burning_pact_curse_seal_consumption"))
    $Interface/Panel/Margin/VBox/LimitBreak.pressed.connect(_play.bind("limit_break_sharingan_curse_overdrive"))
    $Interface/Panel/Margin/VBox/FiendFire.pressed.connect(_play.bind("fiend_fire_dragon_flame_annihilation"))
    $Interface/Panel/Margin/VBox/PulseFlame.pressed.connect(_pulse_flame)
    $Interface/Panel/Margin/VBox/ClearStates.pressed.connect(_clear_states)
    $Interface/Panel/Margin/VBox/Cancel.pressed.connect(_cancel)
    director.timeline_started.connect(_on_started)
    director.impact.connect(_on_impact)
    director.timeline_completed.connect(_on_completed)
    director.timeline_failed.connect(_on_failed)
    status_label.text = "Ready — choose a card timeline"

func _play(animation_id: String) -> void:
    var variant := "base"
    if low_flash_toggle.button_pressed:
        variant = "low_flash"
    elif fast_toggle.button_pressed:
        variant = "fast"
    elif empowered_toggle.button_pressed:
        variant = "empowered"

    var hit_count := 1
    var energy_spent := 1
    var exhausted_card_count := 1
    var strength := 10 if empowered_toggle.button_pressed else 3
    if animation_id == "whirlwind_chidori_blade_storm":
        energy_spent = 5 if empowered_toggle.button_pressed else 3
        hit_count = energy_spent
    elif animation_id == "fiend_fire_dragon_flame_annihilation":
        exhausted_card_count = 7 if empowered_toggle.button_pressed else 4
        hit_count = exhausted_card_count
    elif animation_id == "burning_pact_curse_seal_consumption":
        exhausted_card_count = 3 if empowered_toggle.button_pressed else 1

    status_label.text = "Loading %s (%s, %d impacts)…" % [animation_id, variant, hit_count]
    director.play_timeline(animation_id, variant, {
        "low_flash": low_flash_toggle.button_pressed,
        "quality_scale": 1.0,
        "strength": strength,
        "target_count": 3,
        "energy_spent": energy_spent,
        "exhausted_card_count": exhausted_card_count,
        "hit_count": hit_count
    })

func _pulse_flame() -> void:
    var pulsed := director.pulse_visual_state("flame_barrier_guard", {"scale": 1.20, "duration": 0.24})
    status_label.text = "Flame Barrier reactive pulse" if pulsed else "Flame Barrier state is not active"

func _clear_states() -> void:
    director.clear_all_visual_states()
    status_label.text = "All persistent visual states cleared"

func _cancel() -> void:
    director.cancel_current()
    status_label.text = "Cancelled — committed states: %d" % director.active_visual_state_count()

func _on_started(animation_id: String, variant: String) -> void:
    status_label.text = "Playing %s — %s" % [animation_id, variant]

func _on_impact(animation_id: String, impact_index: int) -> void:
    status_label.text = "%s — impact %d" % [animation_id, impact_index + 1]

func _on_completed(animation_id: String) -> void:
    status_label.text = "%s completed — states: %d" % [animation_id, director.active_visual_state_count()]

func _on_failed(animation_id: String, reason: String) -> void:
    status_label.text = "%s failed: %s" % [animation_id, reason]
