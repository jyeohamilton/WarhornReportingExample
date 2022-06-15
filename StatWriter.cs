using System.Text.RegularExpressions;

namespace WarhornReporting
{
    /// <summary>
    /// Generates some very specific stats.
    /// </summary>
    internal static class StatWriter
    {
        public static void WriteStats(GraphQueryResponse gqr, string outputFile)
        {
            if (gqr.Data?.EventSessions?.Nodes is null)
            {
                throw new InvalidDataException("no nodes in GraphQueryResponse");
            }

            var venueData = new Dictionary<string, VenueInfo>();
            foreach (var node in gqr.Data.EventSessions.Nodes)
            {
                var venueName = node?.Slot?.Venue?.Name!;

                if (!venueData.ContainsKey(venueName))
                {
                    venueData[venueName] = new VenueInfo();
                }

                // Ignore sessions that aren't active and things that don't have a campaign.
                if (node?.Status != "PUBLISHED" || node?.Scenario?.Campaign is null)
                {
                    continue;
                }

                if (node.Uuid is null ||
                    node.Scenario.Name is null ||
                    node.Scenario.Campaign is null ||
                    node.Scenario.Campaign.Name is null)
                {
                    throw new InvalidDataException("Node data is bad");
                }

                if (!venueData[venueName].SessionsByCampaign.ContainsKey(node.Scenario.Campaign.Name))
                {
                    venueData[venueName].SessionsByCampaign.Add(node.Scenario.Campaign.Name, new List<string>());
                }

                if (!venueData[venueName].CountSeatsByCampaign.ContainsKey(node.Scenario.Campaign.Name))
                {
                    venueData[venueName].CountSeatsByCampaign.Add(node.Scenario.Campaign.Name, 0);
                }

                if (node.PlayerSignups is not null)
                {
                    foreach (var player in node.PlayerSignups)
                    {
                        var playerId = player?.User?.Id;
                        if (playerId is not null)
                        {
                            venueData[venueName].UniqueParticipants.Add(playerId);
                        }
                    }

                    venueData[venueName].CountSeatsByCampaign[node.Scenario.Campaign.Name] += node.PlayerSignups.Count;
                }

                if (node.GmSignups is not null)
                {
                    foreach (var gm in node.GmSignups)
                    {
                        // This session is counted as a number of sessions equal to the number of GMs.
                        venueData[venueName].SessionsByCampaign[node.Scenario.Campaign.Name].Add(node.Scenario.Name);

                        var gmId = gm?.User?.Id;
                        if (gmId is not null)
                        {
                            venueData[venueName].UniqueGMs.Add(gmId);
                            venueData[venueName].UniqueParticipants.Add(gmId);
                        }
                    }

                    venueData[venueName].CountSeatsByCampaign[node.Scenario.Campaign.Name] += node.GmSignups.Count;
                }
            }

            // Aggregate up to lodge level.
            var lodgeInfo = new
            {
                UniqueParticipants =
                    venueData.SelectMany(a => a.Value.UniqueParticipants)
                    .ToHashSet(),
                UniqueGMs =
                    venueData.SelectMany(a => a.Value.UniqueGMs)
                    .ToHashSet(),
                SessionsByCampaign =
                venueData.Select(v => v.Value.SessionsByCampaign)
                    .SelectMany(dict => dict)
                    .GroupBy(kvp => kvp.Key)
                    .Select(grp => new { grp.Key, Items = grp.SelectMany(x => x.Value) })
                    .ToDictionary(en => en.Key, en => new List<string>(en.Items)),
                CountSeatsByCampaign =
                    venueData.Select(v => v.Value.CountSeatsByCampaign)
                    .SelectMany(dict => dict)
                    .GroupBy(kvp => kvp.Key)
                    .Select(grp => new { grp.Key, Value = grp.Aggregate(0, (a, b) => a + b.Value) })
                    .ToDictionary(en => en.Key, en => en.Value)
            };

            using var f = new StreamWriter(outputFile);

            f.WriteLine("-----------------");
            f.WriteLine("LODGE-LEVEL STATS");
            f.WriteLine("-----------------");
            f.WriteLine($"Total venues: {venueData.Keys.Count}");
            f.WriteLine($"Total unique participants: {lodgeInfo.UniqueParticipants.Count}");
            f.WriteLine($"Total unique GMs: {lodgeInfo.UniqueGMs.Count}");
            f.WriteLine($"Total seats: {lodgeInfo.CountSeatsByCampaign.Aggregate(0, (a, b) => a + b.Value)}");
            f.WriteLine($"Total sessions: {lodgeInfo.SessionsByCampaign.Aggregate(0, (a, b) => a + b.Value.Count)}");
            f.WriteLine();
            f.WriteLine("Breakdown by campaign - ");
            foreach (var campaign in lodgeInfo.SessionsByCampaign.Keys)
            {
                f.WriteLine($"\r\n\t{campaign}: {lodgeInfo.SessionsByCampaign[campaign].Count} sessions");
                lodgeInfo.SessionsByCampaign[campaign].Sort();

                var countSessionsByType = new Dictionary<AdventureType, int>();
                foreach (var scenario in lodgeInfo.SessionsByCampaign[campaign])
                {
                    f.WriteLine($"\t\t\t{scenario}");

                    var type = IdentifyAdventureType(scenario);
                    if (!countSessionsByType.ContainsKey(type))
                    {
                        countSessionsByType.Add(type, 0);
                    }
                    ++countSessionsByType[type];
                }

                f.WriteLine("\t\tSession count by type:");
                foreach (var kvp in countSessionsByType)
                {
                    f.WriteLine($"\t\t\t{kvp.Key}: {kvp.Value}");
                }

                f.WriteLine($"\t\tSeats: {lodgeInfo.CountSeatsByCampaign[campaign]}");
            }

            f.WriteLine();
            f.WriteLine("---------------");
            f.WriteLine("STATS PER VENUE");
            f.WriteLine("---------------");
            foreach (var kvp in venueData)
            {
                f.WriteLine($"{kvp.Key}");
                f.WriteLine($"\tUnique participants: {kvp.Value.UniqueParticipants!.Count}");
                f.WriteLine($"\tUnique GMs: {kvp.Value.UniqueGMs!.Count}");
                f.WriteLine($"\tSessions: {kvp.Value.SessionsByCampaign.Aggregate(0, (a, b) => a + b.Value.Count)}");
                f.WriteLine("\tSessions by campaign - ");
                foreach (var campaign in kvp.Value.SessionsByCampaign!.Keys)
                {
                    f.WriteLine($"\t\t{campaign}: {kvp.Value.SessionsByCampaign![campaign].Count}");
                }
                f.WriteLine($"\tSeats: {kvp.Value.CountSeatsByCampaign.Aggregate(0, (a, b) => a + b.Value)}");
                f.WriteLine();
            }
        }

        /// <summary>
        /// Best-effort determination of type of adventure using regexes.
        /// </summary>
        /// <param name="scenario">title of Warhorn scenario.</param>
        /// <returns>Best guess at what kind of adventure it is.</returns>
        private static AdventureType IdentifyAdventureType(string scenario)
        {
            if (Bounty.IsMatch(scenario))
            {
                return AdventureType.Bounty;
            }

            if (Quest.IsMatch(scenario))
            {
                return AdventureType.Quest;
            }

            if (Scenario.IsMatch(scenario))
            {
                return AdventureType.Scenario;
            }

            if (OneShot.IsMatch(scenario))
            {
                return AdventureType.OneShot;
            }

            if (Adventure.IsMatch(scenario))
            {
                return AdventureType.Adventure;
            }

            if (AdventurePathBook.IsMatch(scenario))
            {
                return AdventureType.AdventurePathBook;
            }

            return AdventureType.Unknown;
        }

        private enum AdventureType
        {
            Unknown,
            Bounty,
            Quest,
            Scenario,
            OneShot,
            Adventure,
            AdventurePathBook
        }

        private class VenueInfo
        {
            public VenueInfo()
            {
                UniqueParticipants = new HashSet<string>();
                UniqueGMs = new HashSet<string>();
                SessionsByCampaign = new Dictionary<string, List<string>>();
                CountSeatsByCampaign = new Dictionary<string, int>();
            }

            public HashSet<string> UniqueParticipants { get; set; }

            public HashSet<string> UniqueGMs { get; set; }

            public Dictionary<string, List<string>> SessionsByCampaign { get; set; }

            public Dictionary<string, int> CountSeatsByCampaign { get; set; }
        }

        private static readonly Regex Bounty = new("^(PF2|SF) Bounty .*", RegexOptions.Compiled);
        private static readonly Regex Quest = new("^(PFS2|SFS) Quest .*", RegexOptions.Compiled);
        private static readonly Regex Scenario = new("^(PFS1|PFS2|SFS) (#*[0-9]|Intro).*", RegexOptions.Compiled);
        private static readonly Regex OneShot = new("^(PF2|SF) One-Shot .*", RegexOptions.Compiled);
        private static readonly Regex Adventure = new("^(PF|PF2|SF) (Mod|Adventure).*", RegexOptions.Compiled);
        private static readonly Regex AdventurePathBook = new("^(PF1|PF2|SF) AP .*", RegexOptions.Compiled);
    }
}