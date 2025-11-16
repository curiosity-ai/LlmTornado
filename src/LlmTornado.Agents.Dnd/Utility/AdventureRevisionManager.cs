using System.IO;

namespace LlmTornado.Agents.Dnd.Utility;

internal static class AdventureRevisionManager
{
    public const string ManifestFileName = "revisions.json";
    public const string RevisionsFolderName = "revisions";

    public static AdventureRevisionManifest LoadManifest(string adventureRoot)
    {
        Directory.CreateDirectory(adventureRoot);
        var manifestPath = GetManifestPath(adventureRoot);
        return AdventureRevisionManifest.Load(manifestPath);
    }

    public static AdventureRevisionManifest EnsureManifest(string adventureRoot, string fallbackTitle)
    {
        var manifestPath = GetManifestPath(adventureRoot);
        if (File.Exists(manifestPath))
        {
            var manifest = AdventureRevisionManifest.Load(manifestPath);
            if (string.IsNullOrWhiteSpace(manifest.AdventureTitle))
            {
                manifest.AdventureTitle = fallbackTitle;
                manifest.Save(manifestPath);
            }
            return manifest;
        }

        Directory.CreateDirectory(adventureRoot);
        var revisionsFolder = GetRevisionsFolder(adventureRoot);
        Directory.CreateDirectory(revisionsFolder);

        var manifestToCreate = new AdventureRevisionManifest
        {
            AdventureTitle = fallbackTitle
        };
        var entry = manifestToCreate.AddRevision(label: "Initial import");
        var revisionPath = GetRevisionPath(adventureRoot, entry.RevisionId);
        Directory.CreateDirectory(revisionPath);

        MigrateLegacyContents(adventureRoot, revisionPath);
        manifestToCreate.Save(manifestPath);
        return manifestToCreate;
    }

    public static AdventureRevisionEntry CreateRevision(string adventureRoot, AdventureRevisionManifest manifest, string? sourceRevisionId = null, string? label = null)
    {
        var manifestPath = GetManifestPath(adventureRoot);
        Directory.CreateDirectory(GetRevisionsFolder(adventureRoot));

        var entry = manifest.AddRevision(sourceRevisionId, label);
        var revisionPath = GetRevisionPath(adventureRoot, entry.RevisionId);
        Directory.CreateDirectory(revisionPath);

        if (!string.IsNullOrEmpty(sourceRevisionId))
        {
            var sourcePath = GetRevisionPath(adventureRoot, sourceRevisionId);
            if (Directory.Exists(sourcePath))
            {
                CopyDirectory(sourcePath, revisionPath);
            }
        }

        manifest.Save(manifestPath);
        return entry;
    }

    public static string GetManifestPath(string adventureRoot)
    {
        return Path.Combine(adventureRoot, ManifestFileName);
    }

    public static string GetRevisionsFolder(string adventureRoot)
    {
        return Path.Combine(adventureRoot, RevisionsFolderName);
    }

    public static string GetRevisionPath(string adventureRoot, string revisionId)
    {
        return Path.Combine(GetRevisionsFolder(adventureRoot), revisionId);
    }

    private static void MigrateLegacyContents(string adventureRoot, string revisionPath)
    {
        foreach (var file in Directory.GetFiles(adventureRoot))
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destination = Path.Combine(revisionPath, fileName);
            File.Move(file, destination, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(adventureRoot))
        {
            var dirName = Path.GetFileName(directory);
            if (string.Equals(dirName, RevisionsFolderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destination = Path.Combine(revisionPath, dirName);
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }
            Directory.Move(directory, destination);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        foreach (var directoryPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var targetSubDir = directoryPath.Replace(sourceDir, destinationDir);
            Directory.CreateDirectory(targetSubDir);
        }

        foreach (var filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var targetFile = filePath.Replace(sourceDir, destinationDir);
            var targetDirectory = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }
            File.Copy(filePath, targetFile, overwrite: true);
        }
    }
}

