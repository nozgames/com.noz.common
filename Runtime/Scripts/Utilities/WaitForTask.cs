using System.Threading.Tasks;
using UnityEngine;

namespace NoZ
{
    public class WaitForTask : CustomYieldInstruction
    {
        public override bool keepWaiting => !_task.IsCompleted;

        private Task _task;

        public Task Task => _task;

        public WaitForTask(Task task)
        {
            _task = task;
        }
    }

    public class WaitForTask<T> : CustomYieldInstruction
    {
        public override bool keepWaiting => !_task.IsCompleted;

        private Task<T> _task;

        public T Result => _task.Result;
        public Task<T> Task => _task;

        public WaitForTask(Task<T> task)
        {
            _task = task;
        }
    }
}
