namespace RunHold.Services;

internal static class InputInjectionMarker
{
    public const string AcceptExternalInjectedInputEnvironmentVariable = "RUNHOLD_ACCEPT_EXTERNAL_INJECTED_INPUT_FOR_SMOKE";

    public static readonly UIntPtr RunHoldInput = new(0x52554844);
}
