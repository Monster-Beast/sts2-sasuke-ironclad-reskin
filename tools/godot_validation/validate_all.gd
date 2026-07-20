extends SceneTree

var failures: Array[String] = []
var loaded_scripts := 0
var loaded_scenes := 0

func _init() -> void:
    _scan("res://SasukeIronclad")

    if failures.is_empty():
        print("GODOT_PARSE_OK scripts=%d scenes=%d" % [loaded_scripts, loaded_scenes])
        quit(0)
        return

    for failure in failures:
        push_error(failure)
    push_error("GODOT_PARSE_FAILED failures=%d" % failures.size())
    quit(1)

func _scan(path: String) -> void:
    var directory := DirAccess.open(path)
    if directory == null:
        failures.append("cannot open directory: %s" % path)
        return

    directory.list_dir_begin()
    var entry := directory.get_next()
    while not entry.is_empty():
        if entry.begins_with("."):
            entry = directory.get_next()
            continue

        var resource_path := path.path_join(entry)
        if directory.current_is_dir():
            _scan(resource_path)
        elif entry.ends_with(".gd"):
            _load_resource(resource_path, "script")
        elif entry.ends_with(".tscn"):
            _load_resource(resource_path, "scene")
        entry = directory.get_next()
    directory.list_dir_end()

func _load_resource(path: String, kind: String) -> void:
    var resource := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
    if resource == null:
        failures.append("failed to load %s: %s" % [kind, path])
        return

    if kind == "script":
        loaded_scripts += 1
    else:
        loaded_scenes += 1
