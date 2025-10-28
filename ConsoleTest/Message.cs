using LumDbEngine;

namespace PNMessage
{
    public enum MessageType : uint
    {
        Unknown = 0,
        UserNotify = 1,
        IssueAssigned = 2,
        IssueStatusChanged = 3,
        ProjectRoleChanged = 4,
    }

    public class Message
    {
        public Message()
        {
        }

        public Message(uint projId,uint issueId, uint commentId, uint receiverId, uint senderId, DateTime sendTime, string content, MessageType extraInfo)
        {
            ProjId = projId;
            IssueId = issueId;
            CommentId = commentId;
            ReceiverId = receiverId;
            SenderId = senderId;
            SendTime = sendTime;
            Content = content;
            ExtraInfo = (uint)extraInfo;
        }

        [Id]
        public uint Id { get; set; }

        public uint ProjId { get; set; }
        public uint IssueId { get; set; }

        public uint CommentId { get; set; }

        public uint ReceiverId { get; set; }

        public uint SenderId { get; set; }

        public DateTime SendTime { get; set; }

        public DateTime ReadTime { get; set; } = DateTime.MinValue;
        public uint ExtraInfo { get; set; } = (uint)MessageType.Unknown;

        [StrVar]
        public string Content { get; set; } = string.Empty;
                


    }
}
