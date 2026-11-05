using System;

namespace AlienStory
{
    public enum Stage { Aid = 0, Introduction = 1, Recovery = 2, Scavenger = 3, Battery = 4, Decision = 5, Departed = 6, Stayed = 7, Failed = 8, Judgment = 9, Restitution = 10, BadEnding = 11 }
    public enum StoryAction { RefuseAid, DeliverAid, FinishIntroduction, BeginScavenging, EndScrapVisit, DeliverElectronics, DeliverBattery, Leave, Stay, Apologize, RejectJudgment, DeliverRestitution, ExpireRestitution }

    [Serializable]
    public sealed class StoryState
    {
        public int schemaVersion = 1;
        public Stage stage = Stage.Aid;
        public int refusals;
        public int nextVisitDay = 23;
        public int partsReadyDay = -1;
        public int lastRefusalVisit = -1;
        public int restitutionDeadline = -1;
        public int scavengerVisits;
        public bool revealed;
    }

    public static class StoryCore
    {
        public static bool IsEligible(StoryState s, int day)
        {
            if (s == null || day < 23 || day < s.nextVisitDay) return false;
            return s.stage != Stage.Departed && s.stage != Stage.Stayed && s.stage != Stage.Failed && s.stage != Stage.BadEnding;
        }

        // The caller must verify and commit the requested transaction before a delivery action.
        // visitId identifies a distinct actual visit, not a click or a generated queue entry.
        public static bool Apply(StoryState s, StoryAction action, int day, int visitId)
        {
            if (s == null || day < 1 || visitId < 0) return false;
            switch (action)
            {
                case StoryAction.RefuseAid:
                    if (s.stage != Stage.Aid || day < s.nextVisitDay || visitId == s.lastRefusalVisit) return false;
                    s.lastRefusalVisit = visitId;
                    s.refusals++;
                    if (s.refusals >= 4) { s.stage = Stage.Judgment; s.nextVisitDay = day + 1; }
                    else s.nextVisitDay = day + 1;
                    return true;
                case StoryAction.DeliverAid:
                    if (s.stage != Stage.Aid || day < s.nextVisitDay) return false;
                    s.stage = Stage.Introduction;
                    return true;
                case StoryAction.FinishIntroduction:
                    if (s.stage != Stage.Introduction) return false;
                    s.revealed = true;
                    s.stage = Stage.Recovery;
                    s.nextVisitDay = day + 2;
                    return true;
                case StoryAction.BeginScavenging:
                    if (s.stage != Stage.Recovery || day < s.nextVisitDay) return false;
                    s.stage = Stage.Scavenger;
                    s.partsReadyDay = day + 3;
                    s.nextVisitDay = day + 1;
                    return true;
                case StoryAction.EndScrapVisit:
                    if (s.stage != Stage.Scavenger) return false;
                    s.scavengerVisits++;
                    s.nextVisitDay = day + 2;
                    return true;
                case StoryAction.DeliverElectronics:
                    if (s.stage != Stage.Scavenger || day < s.partsReadyDay) return false;
                    s.stage = Stage.Battery;
                    s.nextVisitDay = day + 2;
                    return true;
                case StoryAction.DeliverBattery:
                    if (s.stage != Stage.Battery || day < s.nextVisitDay) return false;
                    s.stage = Stage.Decision;
                    return true;
                case StoryAction.Leave:
                    if (s.stage != Stage.Decision) return false;
                    s.stage = Stage.Departed;
                    return true;
                case StoryAction.Stay:
                    if (s.stage != Stage.Decision) return false;
                    s.stage = Stage.Stayed;
                    return true;
                case StoryAction.Apologize:
                    if (s.stage != Stage.Judgment || day < s.nextVisitDay) return false;
                    s.stage = Stage.Restitution;
                    s.restitutionDeadline = day + 7;
                    s.nextVisitDay = day;
                    return true;
                case StoryAction.RejectJudgment:
                    if (s.stage != Stage.Judgment || day < s.nextVisitDay) return false;
                    s.stage = Stage.BadEnding;
                    return true;
                case StoryAction.DeliverRestitution:
                    if (s.stage != Stage.Restitution || day > s.restitutionDeadline) return false;
                    s.stage = Stage.Introduction;
                    s.nextVisitDay = day + 1;
                    return true;
                case StoryAction.ExpireRestitution:
                    if (s.stage != Stage.Restitution || day <= s.restitutionDeadline) return false;
                    s.stage = Stage.BadEnding;
                    return true;
                default: return false;
            }
        }

        public static bool CanClaimOre(int currentDay, int nextReadyDay, int freeSlots)
            => currentDay >= nextReadyDay && nextReadyDay > 0 && freeSlots >= 10;
        public static int NextChargeDay(int claimedDay) => checked(claimedDay + 3);
    }
}
