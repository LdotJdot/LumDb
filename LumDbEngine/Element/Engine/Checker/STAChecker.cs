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


        public STChecker(AutoResetEvent autoResetEvent, ThreadLocal<int> callCount,int millisecondsTimeout)

        {
            this.callCount = callCount;
            if (callCount.Value != 0)
            {

                LumException.Throw(LumExceptionMessage.SingleThreadMultiTransaction);


            }

            callCount.Value++;
            this.autoResetEvent = autoResetEvent;


            if (autoResetEvent.WaitOne(millisecondsTimeout) == false)
            {
                LumException.Throw(LumExceptionMessage.TransactionTimeout);
            }
        }

        internal bool disposed = false;
        public void Dispose()
        {
            disposed = true;

            callCount.Value--;
            if (!autoResetEvent.SafeWaitHandle.IsClosed) autoResetEvent.Set();
        }

    }
}
