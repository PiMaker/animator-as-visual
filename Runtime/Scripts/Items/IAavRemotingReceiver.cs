#if UNITY_EDITOR

namespace pi.AnimatorAsVisual
{
    public interface IAavRemotingReceiver
    {
        bool AllowRemoteToggle { get; }
        bool IsButton { get; }
        string ParameterName { get; }
        string FriendlyName { get; }
        bool IsFloatType { get; }
    }
}

#endif