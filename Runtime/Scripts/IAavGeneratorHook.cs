#if UNITY_EDITOR
using UnityEngine;

namespace pi.AnimatorAsVisual
{
    public interface IAavGeneratorHook
    {
        void PreApply(GameObject avatar, AavGenerator generator);
        void Apply(GameObject avatar, AavGenerator generator);

        string Name { get; }
    }
}
#endif