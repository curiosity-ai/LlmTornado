using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.FantasyGenerator;
using LlmTornado.Agents.Dnd.Utility;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.MainMenuState;

public class EditAdventureRunnable : OrchestrationRunnable<MainMenuSelection, bool>
{
    private readonly TornadoApi _client;

    public EditAdventureRunnable(TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
    }

    public override async ValueTask<bool> Invoke(RunnableProcess<MainMenuSelection, bool> input)
    {
        var adventures = Directory.GetDirectories(FantasyEngineConfiguration.GeneratedAdventuresFilePath);
        if (adventures.Length == 0)
        {
            Console.WriteLine("No generated adventures found to edit.");
            return false;
        }

        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine("Edit Generated Adventure:");
        Console.WriteLine(new string('═', 80));

        for (var index = 0; index < adventures.Length; index++)
        {
            var adventureRoot = adventures[index];
            var folderName = Path.GetFileName(adventureRoot);
            Console.WriteLine($"[{index + 1}] - {folderName}");
        }

        Console.Write("Select an adventure to edit (enter number): ");
        var selectionInput = Console.ReadLine();

        if (!int.TryParse(selectionInput, out var selectedIndex) ||
            selectedIndex < 1 ||
            selectedIndex > adventures.Length)
        {
            Console.WriteLine("Invalid selection. Cancelled editing.");
            return false;
        }

        var selectedAdventureRoot = adventures[selectedIndex - 1];
        var defaultTitle = Path.GetFileName(selectedAdventureRoot)?.Replace('_', ' ') ?? "Untitled Adventure";

        var manifest = AdventureRevisionManager.EnsureManifest(selectedAdventureRoot, defaultTitle);
        var revisions = manifest.Revisions
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToList();

        if (revisions.Count == 0)
        {
            Console.WriteLine("No revisions available to edit.");
            return false;
        }

        Console.WriteLine("\nAvailable revisions:");
        for (var index = 0; index < revisions.Count; index++)
        {
            var entry = revisions[index];
            Console.WriteLine($"  [{index + 1}] {entry.Label} ({entry.RevisionId}) - {entry.CreatedAtUtc.ToLocalTime():g}");
        }

        Console.Write($"Select a revision to branch from (default {revisions[0].RevisionId}): ");
        var revisionSelection = Console.ReadLine();

        AdventureRevisionEntry baseRevision;
        if (string.IsNullOrWhiteSpace(revisionSelection))
        {
            baseRevision = revisions[0];
        }
        else if (int.TryParse(revisionSelection, NumberStyles.Integer, CultureInfo.InvariantCulture, out var revisionIndex) &&
                 revisionIndex >= 1 &&
                 revisionIndex <= revisions.Count)
        {
            baseRevision = revisions[revisionIndex - 1];
        }
        else
        {
            Console.WriteLine("Invalid revision selection. Cancelled editing.");
            return false;
        }

        var newRevision = AdventureRevisionManager.CreateRevision(selectedAdventureRoot, manifest, baseRevision.RevisionId);
        FantasyGeneratorConfiguration.SetAdventureContext(
            manifest.AdventureTitle ?? defaultTitle,
            selectedAdventureRoot,
            newRevision.RevisionId);

        Console.WriteLine($"\nCreating revision '{newRevision.RevisionId}' based on '{baseRevision.RevisionId}'...");

        var editorWorkflow = new AdventureRevisionEditorConfiguration(_client ?? FantasyGeneratorConfiguration.CreateGeneratorClient());
        var results = await editorWorkflow.InvokeAsync(true);
        var success = results?.FirstOrDefault() ?? false;

        var revisionPath = AdventureRevisionManager.GetRevisionPath(selectedAdventureRoot, newRevision.RevisionId);
        if (success)
        {
            Console.WriteLine($"\nRevision saved to {revisionPath}");
        }
        else
        {
            Console.WriteLine("\nRevision editing did not complete successfully.");
        }

        return success;
    }
}

