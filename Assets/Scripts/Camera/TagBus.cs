using System;

public static class TagBus
{
    public static event Action<string> OnTag;
    public static void Raise(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        OnTag?.Invoke(tag);
    }
}
