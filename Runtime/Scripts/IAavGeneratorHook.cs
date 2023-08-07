#if UNITY_EDITOR
using UnityEngine;

namespace pi.AnimatorAsVisual
{
    public interface IAavGeneratorHook
    {
        void Apply(GameObject avatar, AavGenerator generator);
    }
}
#endif