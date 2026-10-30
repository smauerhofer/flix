namespace Flix.Core;

public sealed record SnapshotManifest(string Version, string Directory);

// Reference identifiers are scoped by source entity; ACCOUNT values must include a bank/scheme scope.
