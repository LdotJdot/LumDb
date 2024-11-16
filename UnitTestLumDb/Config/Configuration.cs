using LumDbEngine.Element.Engine;

namespace UnitTestLumDb.Config
{
    internal class Configuration
    {
        public static bool MemMode = true;

        public static string GetRandomPath()
        {
            return $"{Path.GetTempPath()}{Path.GetRandomFileName()}";
        }

        public static DbEngine GetDbEngineForTest()
        {
            return new DbEngine();
        }

        public static DbEngine GetDbEngineForTest(string path)
        {
            return new DbEngine(path);
        }
    }
}