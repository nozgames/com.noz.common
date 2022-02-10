using UnityEngine;

namespace NoZ
{
    /// <summary>
    /// Yield instruction that waits for the IsDone property to be set to true
    /// </summary>
    public class WaitForDone : CustomYieldInstruction
    {
        /// <summary>
        /// True if the yield instruction is complete
        /// </summary>
        public bool IsDone { get; set; }

        /// <summary>
        /// Wait until IsDone is true
        /// </summary>
        public override bool keepWaiting => !IsDone;
    }
}
