namespace SasukeIronclad.IntegrationGateTest;

internal static class CombinedProgram
{
    public static int Main()
    {
        RuntimeObservationTests.Run(DateTimeOffset.UtcNow);
        return Program.Main();
    }
}
