namespace LumDbEngine.Element.Engine
{
    /// <summary>
    /// Committed file span. <see cref="FileBytes"/> is the header plus every slot through <see cref="LastPage"/>.
    /// Free pages inside that span still count toward the size until <see cref="DbEngine.Vacuum"/>.
    /// </summary>
    public readonly record struct DbSpaceInfo(uint LastPage, bool HasFreePages, long FileBytes);
}
