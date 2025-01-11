using LumDbEngine.Element.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LumDbEngine.Element.Engine.Checker
{
    internal class STChecker
    {
        private readonly ThreadLocal<int> callCount;
        private readonly AutoResetEvent autoResetEvent; // make sure the singularity of transaction

        public STChecker(AutoResetEvent autoResetEvent, ThreadLocal<int> callCount)
        {
            this.callCount = callCount;
            if (callCount.Value != 0)
            {
                LumException.Throw("In a single thread, the previous transaction should be disposed before starting another one.");

            }

            callCount.Value++;
            this.autoResetEvent = autoResetEvent;
            autoResetEvent.WaitOne();
        }

        public void Dispose()
        {
            callCount.Value--;
            if (!autoResetEvent.SafeWaitHandle.IsClosed) autoResetEvent.Set();
        }

    }
}
