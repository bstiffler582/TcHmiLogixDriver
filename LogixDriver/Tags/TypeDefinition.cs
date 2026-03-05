namespace Logix
{
    public record TypeDefinition(ushort Id, uint Length, string Name = "", List<TypeMemberDefinition>? Members = null);

    public record TypeMemberDefinition(ushort Code, string Name, uint Offset, ushort Dimension, ushort BitOffset);
}
