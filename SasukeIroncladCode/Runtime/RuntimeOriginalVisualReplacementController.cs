using Godot;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimeOriginalVisualReplacementSnapshot(
    bool Requested,
    bool Active,
    bool EverHidden,
    int RestoreCount,
    string LastTransition,
    string? TargetType,
    string? TargetName,
    bool? OriginalVisibleBeforeHide,
    IReadOnlyList<string> Reasons
);

/// <summary>
/// Hides only the exact local Ironclad NCreatureVisuals node selected by the
/// reviewed anchor resolver. The original visibility value is captured before
/// concealment and restored on every failure, combat teardown and disposal.
/// </summary>
public sealed class RuntimeOriginalVisualReplacementController : IDisposable
{
    public const string RequiredTargetType = "MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals";
    public const string RequiredTargetName = "Ironclad";

    private readonly List<string> _reasons = [];
    private Node2D? _target;
    private string? _lastTargetType;
    private string? _lastTargetName;
    private bool? _originalVisibleBeforeHide;
    private bool _active;
    private bool _everHidden;
    private int _restoreCount;
    private string _lastTransition = "waiting_for_verified_anchor";
    private int _disposed;

    public bool Active => Volatile.Read(ref _disposed) == 0 && _active;

    public bool TryHide(Node2D target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Volatile.Read(ref _disposed) != 0)
            return Fail("Replacement controller is already disposed.");

        if (_active && ReferenceEquals(_target, target))
            return ValidateActiveTarget();

        if (_active)
            Restore("replacement_target_changed");

        string typeName = target.GetType().FullName ?? string.Empty;
        string nodeName = target.Name.ToString();
        _lastTargetType = typeName;
        _lastTargetName = nodeName;

        if (!string.Equals(typeName, RequiredTargetType, StringComparison.Ordinal) ||
            !string.Equals(nodeName, RequiredTargetName, StringComparison.Ordinal))
        {
            return Fail(
                $"Replacement requires exact target {RequiredTargetType}/{RequiredTargetName}; " +
                $"resolved {typeName}/{nodeName}.");
        }
        if (!GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
            return Fail("The reviewed local Ironclad visual anchor is no longer in the scene tree.");

        bool originalVisible;
        try
        {
            originalVisible = target.Visible;
        }
        catch
        {
            return Fail("The original Ironclad visibility state could not be read.");
        }
        _originalVisibleBeforeHide = originalVisible;
        if (!originalVisible)
            return Fail("The original Ironclad visual was already hidden by the game or another Mod; replacement remains disabled.");

        try
        {
            target.Visible = false;
            if (target.Visible)
                return Fail("The original Ironclad visual did not accept the canary visibility change.");
        }
        catch
        {
            return Fail("The original Ironclad visual could not be hidden safely.");
        }

        _target = target;
        _active = true;
        _everHidden = true;
        _lastTransition = "original_visual_hidden_after_verified_overlay_playback";
        AddReason("The exact local Ironclad NCreatureVisuals node was hidden after a reviewed Sasuke timeline started successfully.");
        AddReason("The original visibility value was captured and is registered for fail-safe restoration.");
        return true;
    }

    public bool ValidateActiveTarget()
    {
        if (!_active)
            return true;

        Node2D? target = _target;
        if (target is null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
        {
            _active = false;
            _target = null;
            _lastTransition = "target_left_scene_tree";
            AddReason("The hidden Ironclad visual left the scene tree; replacement was deactivated.");
            return false;
        }

        try
        {
            if (!target.Visible)
                return true;
        }
        catch
        {
            _active = false;
            _lastTransition = "target_visibility_read_failed";
            AddReason("The hidden Ironclad visibility state became unreadable; replacement was deactivated.");
            return false;
        }

        _active = false;
        _target = null;
        _lastTransition = "original_visibility_changed_externally";
        AddReason("The original Ironclad became visible outside the replacement controller; the canary stopped managing it.");
        return false;
    }

    public void Restore(string reason)
    {
        Node2D? target = _target;
        bool wasActive = _active;
        _active = false;
        _target = null;

        if (wasActive)
        {
            try
            {
                if (target is not null && GodotObject.IsInstanceValid(target) &&
                    _originalVisibleBeforeHide.HasValue)
                {
                    target.Visible = _originalVisibleBeforeHide.Value;
                }
            }
            catch
            {
                AddReason("Restoring the original Ironclad visibility threw and was contained.");
            }
            _restoreCount++;
        }

        _lastTransition = string.IsNullOrWhiteSpace(reason)
            ? "original_visual_restored"
            : $"original_visual_restored:{SanitizeReason(reason)}";
        AddReason($"Original Ironclad visibility restoration requested: {SanitizeReason(reason)}.");
    }

    public RuntimeOriginalVisualReplacementSnapshot Snapshot(bool requested) => new(
        requested,
        Active,
        _everHidden,
        _restoreCount,
        _lastTransition,
        _lastTargetType,
        _lastTargetName,
        _originalVisibleBeforeHide,
        _reasons.ToArray()
    );

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Restore("replacement_controller_disposed");
    }

    private bool Fail(string reason)
    {
        _active = false;
        _target = null;
        _lastTransition = "replacement_refused";
        AddReason(reason);
        return false;
    }

    private void AddReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return;
        if (_reasons.Count >= 16)
            _reasons.RemoveAt(0);
        _reasons.Add(reason);
    }

    private static string SanitizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "unspecified";
        string sanitized = reason.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 160 ? sanitized : sanitized[..160];
    }
}
