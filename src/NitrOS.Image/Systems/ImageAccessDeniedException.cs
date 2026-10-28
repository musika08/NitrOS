namespace NitrOS.Image.Systems;

using System;

public sealed class ImageAccessDeniedException : InvalidOperationException
{
    public const string DefaultMessage = "Windows refused this DISM change (access denied).";

    public ImageAccessDeniedException(string message = DefaultMessage)
        : base(message)
    {
    }

    public ImageAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
