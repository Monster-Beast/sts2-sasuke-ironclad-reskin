extends SasukeStateVisualDirector
class_name SasukeTransformStateVisualDirector

func _build_state(root: Node2D, style: String, params: Dictionary) -> void:
    if style != "curse_stage_two_aura":
        super._build_state(root, style, params)
        return
    _build_curse_stage_two_aura(root, params)

func _build_curse_stage_two_aura(root: Node2D, params: Dictionary) -> void:
    var radius := float(params.get("radius", 118.0))
    var arcs := clampi(int(params.get("arcs", 3)), 2, 5)
    var marks := clampi(int(params.get("marks", 12)), 6, 20)

    for arc_index in range(arcs):
        var arc := Line2D.new()
        arc.closed = true
        arc.width = maxf(2.0, (6.0 - float(arc_index)) * quality_scale)
        arc.default_color = Color(0.48 + 0.06 * arc_index, 0.14, 0.70, 0.28 if low_flash_mode else 0.62)
        var points := PackedVector2Array()
        var segments := maxi(18, int(36 * quality_scale))
        var arc_radius := radius * (0.62 + 0.18 * arc_index)
        for segment in range(segments):
            var angle := TAU * float(segment) / segments
            var wobble := 1.0 + 0.06 * sin(float(segment * 3 + arc_index))
            points.append(Vector2(cos(angle), sin(angle) * 0.64) * arc_radius * wobble)
        arc.points = points
        root.add_child(arc)

    for index in range(marks):
        var mark := Line2D.new()
        var angle := TAU * float(index) / marks
        var start := Vector2(cos(angle), sin(angle) * 0.64) * radius * 0.48
        var middle := Vector2(cos(angle + 0.22), sin(angle + 0.22) * 0.64) * radius * 0.78
        var finish := Vector2(cos(angle - 0.12), sin(angle - 0.12) * 0.64) * radius
        mark.points = PackedVector2Array([start, middle, finish])
        mark.width = 2.8 * quality_scale
        mark.default_color = Color(0.62, 0.18, 0.82, 0.30 if low_flash_mode else 0.74)
        root.add_child(mark)
