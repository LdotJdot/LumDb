using LumDbEngine;

namespace ConsoleTest.EditChurn
{
    internal static class ChurnTables
    {
        public const string Doc = "churn_doc";
        public const string Comment = "churn_comment";
    }

    /// <summary>
    /// 与 NexusMartEntities 相同：独立文件 + partial，便于 VS 源生成器识别 ILumEntity 实现。
    /// 首次打开若仍有红线，请生成一次解决方案，查看 Generated/*.LumEntity.g.cs。
    /// </summary>
    [LumEntity]
    public partial class ChurnDoc
    {
        [Id] public uint Id { get; set; }
        [Key] public int DocId { get; set; }
        [Str32B] public string Title { get; set; } = "";
        [StrVar] public string Body { get; set; } = "";
        public int Revision { get; set; }
        public int State { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    [LumEntity]
    public partial class ChurnComment
    {
        [Id] public uint Id { get; set; }
        [Key] public int CommentId { get; set; }
        public int DocId { get; set; }
        [StrVar] public string Body { get; set; } = "";
        public int State { get; set; }
    }
}
