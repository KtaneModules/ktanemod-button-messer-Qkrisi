using System;

public class ModHandler
{
    public Type t { get; private set; }
    public bool Inactive { get; private set; }

    public ModHandler(Type type, bool inactive = false)
    {
        t = type;
        Inactive = inactive;
    }
}