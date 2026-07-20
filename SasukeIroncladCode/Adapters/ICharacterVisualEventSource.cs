namespace SasukeIronclad.SasukeIroncladCode.Adapters;

/// <summary>
/// Game-version-specific adapters may emit semantic character presentation
/// requests after their symbols have been verified locally.
/// </summary>
public interface ICharacterVisualEventSource
{
    event Action<CharacterVisualRequest>? CharacterVisualRequested;
}
