using LumDbEngine;

namespace ConsoleTest.NexusMart
{
    internal static class T
    {
        public const string User = "nx_user";
        public const string Sku = "nx_sku";
        public const string Order = "nx_order";
        public const string Line = "nx_line";
        public const string Pay = "nx_pay";
        public const string Ticket = "nx_ticket";
        public const string Coupon = "nx_coupon";
        public const string Audit = "nx_audit";
    }

    internal static class OrderSt
    {
        public const int Created = 0, Paid = 1, Shipped = 2, Completed = 3, Cancelled = 4, Returned = 5;
    }

    internal static class PaySt
    {
        public const int Pending = 0, Ok = 1, Fail = 2, Refund = 3;
    }

    internal static class TicketSt
    {
        public const int Open = 0, Doing = 1, Done = 2, Closed = 3;
    }

    [LumEntity]
    public partial class NxUser
    {
        [Id] public uint Id { get; set; }
        [Key] public int UserNo { get; set; }
        [Str16B] public string Login { get; set; } = "";
        public byte Region { get; set; }
        public int Level { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    [LumEntity]
    public partial class NxSku
    {
        [Id] public uint Id { get; set; }
        [Key] public int SkuCode { get; set; }
        [Str32B] public string Name { get; set; } = "";
        public byte Category { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int WarehouseId { get; set; }
        [Bytes8B] public byte[] Fingerprint { get; set; } = new byte[8];
        [StrVar] public string Desc { get; set; } = "";
        public DateTime UpdatedUtc { get; set; }
    }

    [LumEntity]
    public partial class NxOrder
    {
        [Id] public uint Id { get; set; }
        [Key] public int OrderNo { get; set; }
        public int UserNo { get; set; }
        public int Status { get; set; }
        public decimal PayAmount { get; set; }
        public int ItemCount { get; set; }
        public int WarehouseId { get; set; }
        public DateTime CreatedUtc { get; set; }
        [StrVar] public string Note { get; set; } = "";
    }

    [LumEntity]
    public partial class NxLine
    {
        [Id] public uint Id { get; set; }
        [Key] public int LineNo { get; set; }
        public int OrderNo { get; set; }
        public int SkuCode { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }

    [LumEntity]
    public partial class NxPay
    {
        [Id] public uint Id { get; set; }
        [Key] public int PayNo { get; set; }
        public int OrderNo { get; set; }
        public byte Channel { get; set; }
        public decimal Amount { get; set; }
        public int Status { get; set; }
        public DateTime PaidUtc { get; set; }
    }

    [LumEntity]
    public partial class NxTicket
    {
        [Id] public uint Id { get; set; }
        [Key] public int TicketNo { get; set; }
        public int UserNo { get; set; }
        public int OrderNo { get; set; }
        public int Status { get; set; }
        public byte Priority { get; set; }
        [StrVar] public string Body { get; set; } = "";
        public DateTime UpdatedUtc { get; set; }
    }

    [LumEntity]
    public partial class NxCoupon
    {
        [Id] public uint Id { get; set; }
        [Key]
        [Str16B]
        public string Code { get; set; } = "";
        public int UserNo { get; set; }
        public decimal Discount { get; set; }
        public bool Used { get; set; }
        public DateTime ExpireUtc { get; set; }
    }

    [LumEntity]
    public partial class NxAudit
    {
        [Id] public uint Id { get; set; }
        [Key] public long Seq { get; set; }
        public int Actor { get; set; }
        public int Action { get; set; }
        public int Target { get; set; }
        [StrVar] public string Detail { get; set; } = "";
        public DateTime AtUtc { get; set; }
    }
}
