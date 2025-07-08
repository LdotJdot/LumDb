using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LumDbEngine.Utils.SemaphoreUtils
{
    public static class SemaphoreSlimExtensions
    {     

        public static bool WaitAll(this SemaphoreSlim semaphore, int consumeCount, long millionSeconds)
        {
            if (semaphore == null)
            {
                throw new ArgumentNullException(nameof(semaphore));
            }
            
            var sp=TimeSpan.FromMilliseconds(millionSeconds);

            var count = 0;

           while (count != consumeCount)
           { 
                var success = semaphore.Wait(sp);

                if (!success)
                {
                    // 如果获取失败，释放已经获取的信号量
                    if (count != 0) semaphore.Release(consumeCount - count);

                    return false;
                }
                else
                {
                    count++;
                }
            }

            return true;
        }

        //public static bool ReleaseAll(this SemaphoreSlim semaphore)
        //{
        //    if (semaphore == null)
        //    {
        //        throw new ArgumentNullException(nameof(semaphore));
        //    }
        //    if (semaphore.CurrentCount!=0)
        //    {
        //        semaphore.Release(semaphore.CurrentCount);
        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }


        //}

    }
}
