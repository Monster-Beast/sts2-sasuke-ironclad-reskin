using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

/// <summary>
/// Optional notifications emitted by scene hosts whose playback completes or
/// fails asynchronously after Play has returned. They allow the coordinator to
/// release its active context and invoke the original visual fallback.
/// </summary>
public interface IVisualSceneHostNotifications
{
    event Action<AnimationPlaybackHandle>? PlaybackCompleted;
    event Action<AnimationPlaybackHandle, string>? PlaybackFailed;
}
