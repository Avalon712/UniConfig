using UnityEngine;

namespace UniConfig
{
    public sealed class ConfigLoadOperation : CustomYieldInstruction
    {
        public bool IsDone { get; private set; }
        public override bool keepWaiting => !IsDone;

        internal void MarkCompleted() => IsDone = true;
    }
}