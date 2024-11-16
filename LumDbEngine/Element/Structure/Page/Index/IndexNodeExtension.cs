namespace LumDbEngine.Element.Structure.Page.KeyIndex
{
    /// <summary>
    /// Size 32 bytes
    /// </summary>
    internal static class IndexNodeExtension
    {
        internal static void ReadFromStreamDirectly(this ref IndexNode node, BinaryReader br, uint pageId, byte nodeIndex)
        {
            lock (br.BaseStream)
            {
                var pos = DbHeader.HEADER_SIZE + (long)pageId * BasePage.PAGE_SIZE + IndexPage.HEADER_SIZE;
                pos += nodeIndex * IndexNode.Size;
                br.BaseStream.Seek(pos, SeekOrigin.Begin);
                node.Read(br);
                node.HostPageId = pageId;
            }
        }
    }
}