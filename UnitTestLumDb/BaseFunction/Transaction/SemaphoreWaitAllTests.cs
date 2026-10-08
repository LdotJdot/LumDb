using LumDbEngine.Utils.SemaphoreUtils;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class SemaphoreWaitAllTests
    {
        [TestMethod]
        public void WaitAll_Timeout_ReleasesSlotsAlreadyAcquired()
        {
            using var semaphore = new SemaphoreSlim(32, 32);
            Assert.IsTrue(semaphore.Wait(0));
            Assert.AreEqual(31, semaphore.CurrentCount);

            var completed = semaphore.WaitAll(32, 50);

            Assert.IsFalse(completed);
            Assert.AreEqual(31, semaphore.CurrentCount);

            semaphore.Release();
            Assert.AreEqual(32, semaphore.CurrentCount);
        }
    }
}
