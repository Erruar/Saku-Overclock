namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class WmiCmdListItem
{
    public uint value
    {
        get;
    }
    public string text
    {
        get;
    }

    public bool isSet
    {
        get;
    }

    public WmiCmdListItem(string text, uint value, bool isSet = false)
    {
        this.text = text;
        this.value = value;
        this.isSet = isSet;
    }

    public override string ToString()
    {
        return this.text;
    }
}
