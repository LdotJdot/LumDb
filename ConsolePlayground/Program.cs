using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Repo;

namespace ConsolePlayground
{
    internal class Program
    {
        static long lar = 0;

        private static void Main(string[] args)
        {
            //for(int i = 0; i < 9999999; i++)
            //{
            //    using MemTest.MemChecker memChecker = new MemTest.MemChecker(i.ToString());

            //    var key = new UnmanagedBytes(500);
            //    Console.WriteLine(key.AsSpan()[0]);
            //    key.Dispose();
            //}
            //return;

            //ConcurrentDictionary<uint,BasePage> pgDict = new ConcurrentDictionary<uint,BasePage>();
            Dictionary<uint, BasePage> pgDict = new Dictionary<uint, BasePage>();
            List<BasePage> pgList = new List<BasePage>();
            var db = new DbEngine();
            using var ts = db.StartTransaction();
            for (int i = 0; i < 10000; i++)
            {
                // using MemTest.MemChecker memChecker = new MemTest.MemChecker(i.ToString());

                var pg = CreatePage<RepoPage>();
                //pg.Dispose();
                pgList.Add(pg);
                //Console.WriteLine("node balance:" + RepoNodeTmp.count);
            }
            ;
            Console.WriteLine("FINISH!");
        }

        static T CreatePage<T>() where T : BasePage, new()
        {
            T page = (T)new T().Initialize(5);
            return page;
        }
    }
}