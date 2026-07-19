using Aman.Shared.Branding;

namespace Aman.Shared.Container;

public sealed class ContainerBuildRequest
{
    public Guid PackageId { get; init; } = Guid.NewGuid();
    public required string Password { get; init; }
    public int KdfIterations { get; init; } = Crypto.KeyDerivation.DefaultIterations;

    /// <summary>If true, a <c>VendorIdentity</c> signer must be passed to <see cref="ContainerWriter.BuildAsync"/>.
    /// The vendor's public key is always taken from that signer — never set it independently, or a package built
    /// with one identity could carry a public key that doesn't match the private key that will issue its activation codes.</summary>
    public bool RequireActivationCode { get; init; }

    public DateTime? ExpiresUtc { get; init; }
    public int? MaxRuns { get; init; }

    public required BrandingInfo Branding { get; init; }
    public required IReadOnlyList<VideoSourceItem> Videos { get; init; }
    public int ChunkSize { get; init; } = ContainerConstants.DefaultChunkSize;
}
