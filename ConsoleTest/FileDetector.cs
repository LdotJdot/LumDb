namespace ConsoleTest
{
    internal class FileDetector
    {
        public static byte[] Check(string path)
        {
            using var fs = new FileStream(path, FileMode.Open);
            var bt = new byte[fs.Length];
            fs.Read(bt, 0, (int)fs.Length);
            return bt;
        }
    }
}