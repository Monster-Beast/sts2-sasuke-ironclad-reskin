namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed class AnimationPlaybackHandle
{
    public AnimationPlaybackHandle(string cardId, string animationId)
    {
        CardId = cardId;
        AnimationId = animationId;
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }
    public string CardId { get; }
    public string AnimationId { get; }
    public bool IsReleased { get; private set; }

    public void MarkReleased() => IsReleased = true;
}
