using Aman.Shared.Container;

namespace Aman.Shared.Branding;

/// <summary>White-label appearance for the exported Player.</summary>
public sealed class BrandingInfo
{
    public string Title { get; init; } = "AMAN";
    public ContainerConstants.ThemeMode Theme { get; init; } = ContainerConstants.ThemeMode.Dark;
    public string AccentColorHex { get; init; } = "#2E7D32";
    public byte[] LogoPng { get; init; } = Array.Empty<byte>();

    internal void WriteTo(BinaryWriter w)
    {
        w.Write(Title);
        w.Write((byte)Theme);
        w.Write(AccentColorHex);
        w.Write(LogoPng.Length);
        w.Write(LogoPng);
    }

    internal static BrandingInfo ReadFrom(BinaryReader r)
    {
        string title = r.ReadString();
        var theme = (ContainerConstants.ThemeMode)r.ReadByte();
        string accent = r.ReadString();
        int logoLen = r.ReadInt32();
        byte[] logo = r.ReadBytes(logoLen);

        return new BrandingInfo
        {
            Title = title,
            Theme = theme,
            AccentColorHex = accent,
            LogoPng = logo,
        };
    }
}
