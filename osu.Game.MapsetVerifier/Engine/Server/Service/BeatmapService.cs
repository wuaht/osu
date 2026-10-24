using System.Text.RegularExpressions;
using MapsetVerifier.Framework;
using MapsetVerifier.Framework.Objects;
using MapsetVerifier.Parser.Objects;
using MapsetVerifier.Server.Model;
using Serilog;

namespace MapsetVerifier.Server.Service;

public static class BeatmapService
{
    public static ApiBeatmapStructure GetBeatmapSetStructure(string beatmapSetFolder)
    {
        var beatmapSet = CreateBeatmapSet(beatmapSetFolder);
        return BuildBeatmapStructure(beatmapSet);
    }

    public static (BeatmapSet BeatmapSet, ApiBeatmapStructure Structure) ParseBeatmapSet(
        string beatmapSetFolder
    )
    {
        var beatmapSet = CreateBeatmapSet(beatmapSetFolder);
        return (beatmapSet, BuildBeatmapStructure(beatmapSet));
    }

    public static ApiBeatmapSetCheckResult RunBeatmapSetChecks(
        string beatmapSetFolder,
        IProgress<CheckProgress>? progress = null,
        bool includeCheckRunDelta = true,
        bool createSnapshot = true,
        bool includeCheckTimings = false
    )
    {
        var (beatmapSet, _) = ParseBeatmapSet(beatmapSetFolder);
        return RunBeatmapSetChecks(
            beatmapSet,
            progress,
            includeCheckRunDelta,
            createSnapshot,
            includeCheckTimings
        );
    }

    public static ApiBeatmapSetCheckResult RunBeatmapSetChecks(
        BeatmapSet beatmapSet,
        IProgress<CheckProgress>? progress = null,
        bool includeCheckRunDelta = true,
        bool createSnapshot = true,
        bool includeCheckTimings = false
    )
    {
        if (createSnapshot)
        {
            try
            {
                SnapshotService.SnapshotCurrentBeatmapSet(beatmapSet);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to snapshot beatmap set before running checks.");
            }
        }

        var issues = Checker.GetBeatmapSetIssues(
            beatmapSet,
            progress,
            includeCheckTimings,
            out var checkTimings
        );
        var result = BuildBeatmapSetCheckResult(beatmapSet, issues);
        var delta = includeCheckRunDelta
            ? CheckRunHistoryService.BuildDeltaAndRememberCurrent(beatmapSet, result)
            : null;

        return new ApiBeatmapSetCheckResult(
            general: result.General,
            difficulties: result.Difficulties,
            checks: result.Checks,
            checkRunDelta: delta,
            checkTimings: checkTimings
        );
    }

    private static BeatmapSet CreateBeatmapSet(string beatmapSetFolder)
    {
        var beatmapSet = new BeatmapSet(beatmapSetFolder);
        beatmapSet.ClearInterpretedDifficultyOverrides();
        return beatmapSet;
    }

    private static ApiBeatmapStructure BuildBeatmapStructure(BeatmapSet beatmapSet) =>
        new(
            beatmapSet
                .Beatmaps.Select(beatmap => new ApiBeatmapStructureDifficulty(
                    category: beatmap.MetadataSettings.version,
                    beatmapId: beatmap.MetadataSettings.beatmapId,
                    mode: beatmap.GeneralSettings.mode,
                    starRating: beatmap.StarRating
                ))
                .ToList()
        );

    private static ApiBeatmapSetCheckResult BuildBeatmapSetCheckResult(
        BeatmapSet beatmapSet,
        List<Issue> issues
    )
    {
        var checksById = CheckerRegistry.GetChecksWithId();

        var setWideBeatmapSetIssues = issues
            .Where(issue => issue.CheckOrigin is BeatmapSetCheck && issue.beatmap == null)
            .ToArray();

        var generalIssues = issues
            .Where(issue => issue.CheckOrigin is GeneralCheck)
            .Concat(setWideBeatmapSetIssues)
            .ToArray();

        var apiBeatmapCheckResults = beatmapSet
            .Beatmaps.Select(beatmap =>
            {
                var interpretedDifficulty = beatmap.GetDifficulty();
                var beatmapIssues = issues
                    .Where(issue => issue.beatmap == beatmap)
                    .Except(generalIssues)
                    .Where(issue => issue.AppliesToDifficulty(interpretedDifficulty));

                var beatmapCheckResults = beatmapIssues.Select(issue =>
                {
                    var checkId = checksById
                        .FirstOrDefault(c => c.Value.GetType() == issue.CheckOrigin?.GetType())
                        .Key;

                    return new ApiCheckResult(
                        id: checkId,
                        level: issue.level,
                        message: issue.message,
                        ruleIds: RankingCriteriaService.RuleIdsFor(issue)
                    );
                });

                var parsedDifficulty =
                    interpretedDifficulty == Beatmap.Difficulty.Ultra
                        ? Beatmap.Difficulty.Expert
                        : interpretedDifficulty;

                return new ApiCategoryCheckResult(
                    checkResults: beatmapCheckResults,
                    category: beatmap.MetadataSettings.version,
                    beatmapId: beatmap.MetadataSettings.beatmapId,
                    mode: beatmap.GeneralSettings.mode,
                    difficultyLevel: parsedDifficulty,
                    starRating: beatmap.StarRating
                );
            })
            .ToList();

        var generalCheckResults = generalIssues
            .Select(issue =>
            {
                var checkId = checksById
                    .FirstOrDefault(c => c.Value.GetType() == issue.CheckOrigin?.GetType())
                    .Key;

                return new ApiCheckResult(
                    id: checkId,
                    level: issue.level,
                    message: issue.message,
                    ruleIds: RankingCriteriaService.RuleIdsFor(issue)
                );
            })
            .ToList();

        // Build checks dictionary: include checks present in general or any difficulty
        var checksPresentIds = new HashSet<int>(
            generalCheckResults
                .Select(c => c.Id)
                .Concat(
                    apiBeatmapCheckResults.SelectMany(cat => cat.CheckResults.Select(cr => cr.Id))
                )
        );
        var checksDict = new Dictionary<int, ApiCheckDefinition>();
        foreach (var id in checksPresentIds)
        {
            if (!checksById.TryGetValue(id, out var check))
                continue;
            var name = check.GetMetadata().Message;
            var difficultiesForCheck = apiBeatmapCheckResults
                .Where(cat => cat.CheckResults.Any(cr => cr.Id == id))
                .Select(cat => cat.Category)
                .Distinct()
                .ToList();
            // Include "General" if appears in general results
            if (generalCheckResults.Any(gr => gr.Id == id))
                difficultiesForCheck.Insert(0, "General");
            checksDict[id] = new ApiCheckDefinition(
                id: id,
                name: name,
                difficulties: difficultiesForCheck
            );
        }

        return new ApiBeatmapSetCheckResult(
            general: new ApiCategoryCheckResult(
                checkResults: generalCheckResults,
                category: "General",
                beatmapId: null,
                mode: null,
                difficultyLevel: null,
                starRating: null
            ),
            difficulties: apiBeatmapCheckResults,
            checks: checksDict
        );
    }

    public static ApiCategoryOverrideCheckResult? RunDifficultyCheckWithOverride(
        string beatmapSetFolder,
        string difficultyName,
        Beatmap.Difficulty overrideDifficulty
    )
    {
        var beatmapSet = new BeatmapSet(beatmapSetFolder);
        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b =>
            b.MetadataSettings.version == difficultyName
        );

        if (beatmap == null)
            return null;

        beatmapSet.ApplyInterpretedDifficultyOverride(beatmap, overrideDifficulty);
        var issues = Checker.GetBeatmapSetIssues(beatmapSet);
        var checksById = CheckerRegistry.GetChecksWithId();

        var generalIssues = issues.Where(issue => issue.CheckOrigin is GeneralCheck).ToArray();

        var beatmapIssues = issues
            .Where(issue => issue.beatmap == beatmap)
            .Except(generalIssues)
            .Where(issue => issue.AppliesToDifficulty(overrideDifficulty));

        var beatmapCheckResults = beatmapIssues
            .Select(issue =>
            {
                var checkId = checksById
                    .FirstOrDefault(c => c.Value.GetType() == issue.CheckOrigin?.GetType())
                    .Key;

                return new ApiCheckResult(
                    id: checkId,
                    level: issue.level,
                    message: issue.message,
                    ruleIds: RankingCriteriaService.RuleIdsFor(issue)
                );
            })
            .ToList();

        // Build checks dictionary: include checks present in general or any difficulty
        var checksPresentIds = new HashSet<int>(beatmapCheckResults.Select(c => c.Id));
        var checksDict = new Dictionary<int, ApiCheckDefinition>();
        foreach (var id in checksPresentIds)
        {
            if (!checksById.TryGetValue(id, out var check))
                continue;
            var name = check.GetMetadata().Message;
            checksDict[id] = new ApiCheckDefinition(
                id: id,
                name: name,
                difficulties: [difficultyName]
            );
        }

        return new ApiCategoryOverrideCheckResult(
            categoryResult: new ApiCategoryCheckResult(
                checkResults: beatmapCheckResults,
                category: beatmap.MetadataSettings.version,
                beatmapId: beatmap.MetadataSettings.beatmapId,
                mode: beatmap.GeneralSettings.mode,
                difficultyLevel: overrideDifficulty,
                starRating: beatmap.StarRating
            ),
            checks: checksDict
        );
    }
}
