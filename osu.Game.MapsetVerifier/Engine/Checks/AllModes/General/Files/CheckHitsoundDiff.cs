using MapsetVerifier.Framework.Objects;
using MapsetVerifier.Framework.Objects.Attributes;
using MapsetVerifier.Framework.Objects.Metadata;
using MapsetVerifier.Parser.Objects;

namespace MapsetVerifier.Checks.AllModes.General.Files
{
    [Check]
    public class CheckHitsoundDiff : BeatmapSetCheck
    {
        public override CheckMetadata GetMetadata() =>
            new BeatmapCheckMetadata
            {
                Category = "Files",
                Message = "Hitsound difficulty present.",
                Author = "RandomeLoL",

                Documentation = new Dictionary<string, string>
                {
                    {
                        "Purpose",
                        @"
                    Beatmaps must not be nominated with hitsound difficulties still present."
                    },
                    {
                        "Reasoning",
                        @"
                    Hitsounding template difficulties are commonly used as an easy way to copy and apply hitsounds across all difficulties of the beatmapset. However, they must be deleted before nominating the beatmapset.
                    "
                    },
                },
            };

        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>
            {
                {
                    "HitsoundDiff",
                    new IssueTemplate(
                        Issue.Level.Problem,
                        "{0} may be a hitsound difficulty. If it were the case, ensure it is deleted before nominating this beatmap.",
                        "difficulty"
                    ).WithCause("Potential Hitsound difficulty detected.")
                },
            };
        }

        public override IEnumerable<Issue> GetIssues(BeatmapSet beatmapSet)
        {
            // hitsound difficulties are excluded from the beatmaps when parsing the set.
            foreach (string difficulty in beatmapSet.HitsoundDifficulties)
                yield return new Issue(GetTemplate("HitsoundDiff"), null, difficulty);
        }
    }
}
