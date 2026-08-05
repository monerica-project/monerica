using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DetailChecker.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Starting DetailChecker");

// Carry at most this many related links forward (matches the edit/approval cap).
const int MaxRelatedLinks = 3;
const string NoteMarker = "[auto-detail-check";

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile(DirectoryManager.Common.Constants.StringConstants.AppSettingsFileName, optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
    .Build();

var userAgent = config["UserAgent:Header"] ?? "MonericaDetailChecker/1.0";
var torHost = config["TorProxy:Host"] ?? "127.0.0.1";
var torPort = int.TryParse(config["TorProxy:Port"], out var tp) ? tp : 9050;

bool Flag(string key, bool dflt) => bool.TryParse(config[key], out var b) ? b : dflt;
int Num(string key, int dflt) => int.TryParse(config[key], out var n) ? n : dflt;

var enabled = Flag("DetailCheck:Enabled", true);
var dryRun = Flag("DetailCheck:DryRun", true);          // SAFE default: write nothing until told to.
var maxEntries = Num("DetailCheck:MaxEntries", 0);       // 0 = all
var concurrency = Math.Max(1, Num("DetailCheck:Concurrency", 4));

var ollamaEnabled = Flag("Ollama:Enabled", true);
var ollamaEndpoint = config["Ollama:Endpoint"] ?? "http://127.0.0.1:11434";
var ollamaModel = config["Ollama:Model"] ?? "llama3.2:1b";

if (!enabled)
{
    Console.WriteLine("DetailCheck:Enabled=false — nothing to do.");
    return;
}

var services = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options => DbProvider.Configure(options, config))
    .AddDbRepositories()
    .BuildServiceProvider();

using var fetcher = new ContentFetcher(userAgent, torHost, torPort);

OllamaClient? ollama = null;
if (ollamaEnabled)
{
    ollama = new OllamaClient(ollamaEndpoint, ollamaModel);
    if (!await ollama.IsReachableAsync())
    {
        Console.WriteLine($"WARNING: Ollama not reachable at {ollamaEndpoint} — running rules-only (email/contact); country/KYC will be skipped.");
        ollama = null;
    }
    else
    {
        Console.WriteLine($"Ollama OK: {ollamaEndpoint} model={ollamaModel}");
    }
}

Console.WriteLine($"Mode: {(dryRun ? "DRY-RUN (no writes)" : "WRITE (creates pending revisions)")}, concurrency={concurrency}, maxEntries={(maxEntries == 0 ? "all" : maxEntries.ToString())}");

var entriesRepo = services.GetRequiredService<IDirectoryEntryRepository>();
var active = (await entriesRepo.GetAllActiveEntries())
    .Where(e => !string.IsNullOrWhiteSpace(e.Link))
    .ToList();
if (maxEntries > 0)
{
    active = active.Take(maxEntries).ToList();
}

Console.WriteLine($"Active entries to reconfirm: {active.Count}");

var semaphore = new SemaphoreSlim(concurrency);
var proposed = 0;
var skipped = 0;
var offline = 0;

var tasks = active.Select(async entry =>
{
    await semaphore.WaitAsync();
    try
    {
        var result = await ProcessAsync(entry, services, fetcher, ollama, dryRun);
        switch (result)
        {
            case ProcessResult.Proposed: Interlocked.Increment(ref proposed); break;
            case ProcessResult.Offline: Interlocked.Increment(ref offline); break;
            default: Interlocked.Increment(ref skipped); break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{entry.DirectoryEntryId}] ERROR: {ex.Message}");
        Interlocked.Increment(ref skipped);
    }
    finally
    {
        semaphore.Release();
    }
});

await Task.WhenAll(tasks);

Console.WriteLine("-----------------");
Console.WriteLine($"Done. Proposed revisions: {proposed}, offline/skipped: {offline}, no-change: {skipped}. {(dryRun ? "(dry-run — nothing written)" : string.Empty)}");

async Task<ProcessResult> ProcessAsync(
    DirectoryEntry entry,
    IServiceProvider root,
    ContentFetcher contentFetcher,
    OllamaClient? model,
    bool isDryRun)
{
    using var scope = root.CreateScope();
    var submissionRepo = scope.ServiceProvider.GetRequiredService<ISubmissionRepository>();
    var entryTagRepo = scope.ServiceProvider.GetRequiredService<IDirectoryEntryTagRepository>();
    var additionalLinkRepo = scope.ServiceProvider.GetRequiredService<IAdditionalLinkRepository>();

    var text = await contentFetcher.FetchSiteTextAsync(entry.Link);
    if (text is null)
    {
        Console.WriteLine($"[{entry.DirectoryEntryId}] {entry.Link} → not reachable, skipping (SiteChecker handles offline).");
        return ProcessResult.Offline;
    }

    LlmResult? llm = null;
    if (model is not null)
    {
        // Give the model the actual prose from the terms/privacy/faq/about pages (capped),
        // and let it reason about the KYC stance + jurisdiction — sites don't state these
        // verbatim, so we don't pre-filter by keyword.
        var relevant = SnippetExtractor.RelevantTextForModel(text);
        llm = await model.ClassifyAsync(relevant);
    }

    var decision = DetailAnalyzer.Analyze(entry, text, llm);
    if (!decision.HasAnything)
    {
        return ProcessResult.NoChange;
    }

    // De-dup: never stack on an existing pending submission for this listing.
    var existing = await submissionRepo.GetByLinkAndStatusAsync(entry.Link, SubmissionStatus.Pending);
    if (existing is not null)
    {
        Console.WriteLine($"[{entry.DirectoryEntryId}] {entry.Name}: has a pending submission already — skipping.");
        return ProcessResult.NoChange;
    }

    var note = BuildNote(decision);
    Console.WriteLine($"[{entry.DirectoryEntryId}] {entry.Name}: {decision.Changes.Count} change(s), {decision.Uncertainties.Count} flag(s)");
    Console.WriteLine($"    {note.Replace("\n", "\n    ")}");

    if (isDryRun)
    {
        return ProcessResult.Proposed;
    }

    var submission = await BuildRevisionAsync(entry, decision, note, entryTagRepo, additionalLinkRepo);
    await submissionRepo.CreateAsync(submission);
    return ProcessResult.Proposed;
}

string BuildNote(DetailDecision decision)
{
    var sb = new System.Text.StringBuilder();
    sb.Append(NoteMarker).Append(' ').Append(DateTime.UtcNow.ToString("yyyy-MM-dd")).Append(']');
    if (decision.Changes.Count > 0)
    {
        sb.Append("\nProposed: ").Append(string.Join(
            "; ",
            decision.Changes.Select(c => $"{c.Field} {c.OldValue ?? "none"}→{c.NewValue} {c.Evidence}".Trim())));
    }

    if (decision.Uncertainties.Count > 0)
    {
        sb.Append("\nReview (no change made): ").Append(string.Join(" | ", decision.Uncertainties));
    }

    return sb.ToString().Trim();
}

async Task<Submission> BuildRevisionAsync(
    DirectoryEntry entry,
    DetailDecision decision,
    string note,
    IDirectoryEntryTagRepository entryTagRepo,
    IAdditionalLinkRepository additionalLinkRepo)
{
    // Carry tags + related links forward exactly like the offline auto-submission, so that
    // approving this revision never blanks them (they live in their own tables).
    var entryTags = await entryTagRepo.GetTagsForEntryAsync(entry.DirectoryEntryId);
    var tagNames = string.Join(
        ", ",
        entryTags.Select(t => t.Name).Where(n => !string.IsNullOrWhiteSpace(n)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
    var selectedTagIdsCsv = string.Join(",", entryTags.Select(t => t.TagId).Distinct());

    var additional = await additionalLinkRepo.GetByDirectoryEntryIdAsync(entry.DirectoryEntryId);
    var relatedLinks = (additional ?? new List<AdditionalLink>())
        .OrderBy(x => x.SortOrder).ThenBy(x => x.AdditionalLinkId)
        .Select(x => x.Link)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(MaxRelatedLinks)
        .ToList();

    var submission = new Submission
    {
        SubmissionStatus = SubmissionStatus.Pending,
        DirectoryEntryId = entry.DirectoryEntryId,
        SubCategoryId = entry.SubCategoryId,
        DirectoryStatus = entry.DirectoryStatus, // a detail revision — keep the current status
        Name = entry.Name,
        Link = entry.Link,
        Link2 = entry.Link2,
        Link3 = entry.Link3,
        Description = entry.Description,
        Location = entry.Location,
        Processor = entry.Processor,
        CountryCode = entry.CountryCode,
        PgpKey = entry.PgpKey,
        ProofLink = entry.ProofLink,
        VideoLink = entry.VideoLink,
        FoundedDate = entry.FoundedDate,
        Email = entry.Email,
        Messenger = entry.Messenger,
        Social = entry.Social,
        KycPolicy = entry.KycPolicy,
        Note = entry.Note,
        NoteToAdmin = note,
        Tags = string.IsNullOrWhiteSpace(tagNames) ? null : tagNames,
        SelectedTagIdsCsv = entryTags.Count == 0 ? null : selectedTagIdsCsv,
        RelatedLinks = relatedLinks,
        SuggestedSubCategory = null,
        IpAddress = null,
    };

    // Apply the confident, auto-fillable changes (country / KYC / email).
    foreach (var change in decision.Changes)
    {
        switch (change.Field)
        {
            case "CountryCode":
                submission.CountryCode = change.NewValue;
                break;
            case "Email":
                submission.Email = change.NewValue;
                break;
            case "KycPolicy":
                submission.KycPolicy = DetailAnalyzer.KycFromLlm(MapKycLabelToToken(change.NewValue));
                break;
        }
    }

    return submission;
}

// The note stores the human label ("Guaranteed No KYC"); translate back to the token the
// mapper understands so the submission's KycPolicy enum is set correctly.
static string MapKycLabelToToken(string? label) => label switch
{
    "Guaranteed No KYC" => "guaranteed_no",
    "Rare KYC" => "rare",
    "Shotgun KYC" => "shotgun",
    "Mandatory KYC" => "mandatory",
    "Varies By Provider" => "varies",
    _ => "not_stated",
};

internal enum ProcessResult
{
    NoChange,
    Proposed,
    Offline,
}
