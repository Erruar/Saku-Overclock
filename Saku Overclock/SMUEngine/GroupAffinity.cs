namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
internal readonly struct GroupAffinity
{
    public static GroupAffinity Undefined = new(ushort.MaxValue, 0uL);

    public ushort Group
    {
        get;
    }

    public ulong Mask
    {
        get;
    }

    public GroupAffinity(ushort group, ulong mask)
    {
        Group = group;
        Mask = mask;
    }

    public static GroupAffinity Single(ushort group, int index)
    {
        return new GroupAffinity(group, (ulong)(1L << index));
    }

    public override bool Equals(object? o)
    {
        if (o == null || (object)GetType() != o.GetType())
        {
            return false;
        }
        var groupAffinity = (GroupAffinity)o;
        return Group == groupAffinity.Group && Mask == groupAffinity.Mask;
    }

    public override int GetHashCode()
    {
        return Group.GetHashCode() ^ Mask.GetHashCode();
    }

    public static bool operator ==(GroupAffinity a1, GroupAffinity a2)
    {
        return a1.Group == a2.Group && a1.Mask == a2.Mask;
    }

    public static bool operator !=(GroupAffinity a1, GroupAffinity a2)
    {
        return a1.Group != a2.Group || a1.Mask != a2.Mask;
    }
}