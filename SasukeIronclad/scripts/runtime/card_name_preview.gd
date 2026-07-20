extends Control

@export_file("*.json") var name_catalog_path := "res://SasukeIronclad/data/card_name_overrides.json"
@export_file("*.json") var surface_catalog_path := "res://SasukeIronclad/data/card_title_surfaces.json"

@onready var locale_select: OptionButton = $Margin/VBox/Controls/Locale
@onready var surface_select: OptionButton = $Margin/VBox/Controls/Surface
@onready var upgraded_toggle: CheckButton = $Margin/VBox/Controls/Upgraded
@onready var grid: GridContainer = $Margin/VBox/Scroll/Grid
@onready var summary: Label = $Margin/VBox/Summary

var _name_catalog: Dictionary = {}
var _surface_catalog: Dictionary = {}

func _ready() -> void:
    _name_catalog = _load_json(name_catalog_path)
    _surface_catalog = _load_json(surface_catalog_path)
    locale_select.add_item("中文")
    locale_select.set_item_metadata(0, "zh-CN")
    locale_select.add_item("English")
    locale_select.set_item_metadata(1, "en-US")
    for surface in _surface_catalog.get("surfaces", []):
        var index := surface_select.item_count
        surface_select.add_item(String(surface.get("id", "unknown")))
        surface_select.set_item_metadata(index, String(surface.get("id", "")))
    locale_select.item_selected.connect(_refresh.unbind(1))
    surface_select.item_selected.connect(_refresh.unbind(1))
    upgraded_toggle.toggled.connect(_refresh.unbind(1))
    _refresh()

func _refresh() -> void:
    for child in grid.get_children():
        child.queue_free()
    for heading in ["card_id", "original", "Sasuke title", "kind", "layout"]:
        _add_cell(heading)
    var locale := String(locale_select.get_item_metadata(locale_select.selected))
    var surface_id := String(surface_select.get_item_metadata(surface_select.selected))
    var surface := _find_surface(surface_id)
    var limit := float((surface.get("width_units", {}) as Dictionary).get(locale, 32.0))
    var max_lines := int(surface.get("max_lines", 2))
    var upgraded := upgraded_toggle.button_pressed
    var adjustments := 0
    var provisional := 0
    for card in _name_catalog.get("cards", []):
        var item: Dictionary = card
        var original := String((item.get("original_name", {}) as Dictionary).get(locale, item.get("card_id", "")))
        var display := String((item.get("display_name", {}) as Dictionary).get(locale, original))
        if upgraded:
            original += "+"
            display += "+"
        var units := _estimate_title_units(display)
        var status := _layout_status(units, limit, max_lines)
        if status != "OK":
            adjustments += 1
        if String(item.get("rename_status", "")) == "provisional":
            provisional += 1
        _add_cell(String(item.get("card_id", "")))
        _add_cell(original)
        _add_cell(display)
        _add_cell("%s / %s" % [item.get("card_kind", "normal"), item.get("rename_status", "")])
        _add_cell("%s %.1f/%.1f" % [status, units, limit])
    summary.text = "%s · %s · %d cards · %d adjustments · %d provisional" % [locale, surface_id, int((_name_catalog.get("cards", []) as Array).size()), adjustments, provisional]

func _add_cell(value: String) -> void:
    var label := Label.new()
    label.text = value
    label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
    label.custom_minimum_size = Vector2(150, 36)
    grid.add_child(label)

func _find_surface(surface_id: String) -> Dictionary:
    for surface in _surface_catalog.get("surfaces", []):
        if String((surface as Dictionary).get("id", "")) == surface_id:
            return surface
    return {}

func _layout_status(units: float, limit: float, max_lines: int) -> String:
    if units <= limit:
        return "OK"
    if units <= limit / 0.72:
        return "SCALE"
    if max_lines > 1 and units <= limit * float(max_lines):
        return "WRAP"
    return "ORIGINAL FALLBACK"

func _estimate_title_units(value: String) -> float:
    var total := 0.0
    for character in value:
        var codepoint := character.unicode_at(0)
        if character == " ":
            total += 0.5
        elif codepoint >= 0x2E80:
            total += 2.0
        elif character in ["·", ":", "-", "+"]:
            total += 0.75
        else:
            total += 1.0
    return total

func _load_json(path: String) -> Dictionary:
    if not FileAccess.file_exists(path):
        return {}
    var file := FileAccess.open(path, FileAccess.READ)
    if file == null:
        return {}
    var parsed = JSON.parse_string(file.get_as_text())
    return parsed if parsed is Dictionary else {}
