namespace LumDbEngine.Element.Structure
{
    internal interface IStructure
    {
        internal void Write(BinaryWriter bw);

        internal void Read(BinaryReader br);
    }
}