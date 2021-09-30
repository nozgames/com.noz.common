using UnityEngine;

namespace NoZ.Tweenz
{
    public sealed class WaitForTween : CustomYieldInstruction
    {
        private TweenzId _id;

        public WaitForTween(TweenzId id) => _id = id;

        public override bool keepWaiting => Tween.IsDone(_id);
    }
}

