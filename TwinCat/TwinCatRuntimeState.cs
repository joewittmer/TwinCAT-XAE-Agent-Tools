using TwinCAT.Ads;

namespace TwincatMcpServer.TwinCat;

internal enum TwinCatRuntimeState
{
    Config,
    Run
}

internal static class TwinCatRuntimeStateParser
{
    public static TwinCatRuntimeState Parse(string? state)
    {
        if (string.Equals(state, nameof(TwinCatRuntimeState.Config), StringComparison.OrdinalIgnoreCase))
        {
            return TwinCatRuntimeState.Config;
        }

        if (string.Equals(state, nameof(TwinCatRuntimeState.Run), StringComparison.OrdinalIgnoreCase))
        {
            return TwinCatRuntimeState.Run;
        }

        throw new ArgumentException("Runtime state must be 'Config' or 'Run'.", nameof(state));
    }

    public static AdsState ToAdsState(TwinCatRuntimeState state)
    {
        return state switch
        {
            TwinCatRuntimeState.Config => AdsState.Config,
            TwinCatRuntimeState.Run => AdsState.Run,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported runtime state.")
        };
    }
}
