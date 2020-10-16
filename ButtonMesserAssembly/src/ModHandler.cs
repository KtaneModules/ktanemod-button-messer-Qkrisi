using System;

public class ModHandler
{
    public readonly Type t;
    public readonly bool Inactive;

    public ModHandler(Type type, bool inactive = false)
    {
        t = type;
        Inactive = inactive;
    }
}