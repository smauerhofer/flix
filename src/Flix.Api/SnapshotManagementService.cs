using Flix.Core;

internal sealed class SnapshotManagementService(
  FilterManager manager,
  string root,
  string configurationFile,
  string contentRoot,
  ILogger<SnapshotManagementService> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
      try
      {
        FilterParameters parameters = FilterParametersFile.Read(configurationFile);
        string configuredRoot = Path.GetFullPath(parameters.Root, contentRoot);
        if (!Path.GetFullPath(root).Equals(configuredRoot,
          OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
          throw new InvalidOperationException("Changing Root requires restarting the API.");
        }
        SnapshotManifest manifest = SnapshotFiles.Read(root);
        bool changed;
        using (var current = manager.Acquire())
        {
          changed = manifest.Version != current.Snapshot.Version ||
            parameters != current.Filter.Parameters;
        }
        if (changed)
        {
          // Only the management service owns activation and disposal.
          var next = new FilterSnapshot(manifest.Version,
            SnapshotFiles.Resolve(root, manifest));
          try
          {
            manager.Activate(new ScreeningFilter(next, parameters));
          }
          catch
          {
            next.Dispose();
            throw;
          }
          logger.LogInformation("Activated filter version {Version} with YAML parameters", manifest.Version);
        }
        int removed = manager.CleanupRetired();
        if (removed > 0)
        {
          logger.LogInformation("Disposed {Count} retired filters", removed);
        }
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Filter management failed; old filter remains active");
      }
    }
  }
}
