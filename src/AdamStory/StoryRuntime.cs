using System;
using HarmonyLib;
using UnityEngine;

namespace AlienStory
{
    internal static class StoryRuntime
    {
        internal const string AdamId = "personal.adam-story.adam";
        internal const string GodId = "personal.adam-story.god";
        private const string ModId = PortraitRegistry.ModId;
        private static int Day => StoreStation.GetDayCounter();

        internal static StoryState Load()
        {
            int rawStage = ModHelper.GetModDataInt(ModId, "stage", 0);
            if (!Enum.IsDefined(typeof(Stage), rawStage)) throw new InvalidOperationException("Unknown saved story stage " + rawStage);
            return new StoryState {
                stage = (Stage)rawStage,
                refusals = ModHelper.GetModDataInt(ModId, "refusals", 0),
                nextVisitDay = ModHelper.GetModDataInt(ModId, "next_visit", 23),
                partsReadyDay = ModHelper.GetModDataInt(ModId, "parts_ready", -1),
                lastRefusalVisit = ModHelper.GetModDataInt(ModId, "last_refusal", -1),
                restitutionDeadline = ModHelper.GetModDataInt(ModId, "restitution_deadline", -1),
                scavengerVisits = ModHelper.GetModDataInt(ModId, "scavenger_visits", 0),
                revealed = ModHelper.GetModDataBool(ModId, "revealed", false)
            };
        }

        private static void Save(StoryState s)
        {
            ModHelper.SetModDataInt(ModId, "stage", (int)s.stage);
            ModHelper.SetModDataInt(ModId, "refusals", s.refusals);
            ModHelper.SetModDataInt(ModId, "next_visit", s.nextVisitDay);
            ModHelper.SetModDataInt(ModId, "parts_ready", s.partsReadyDay);
            ModHelper.SetModDataInt(ModId, "last_refusal", s.lastRefusalVisit);
            ModHelper.SetModDataInt(ModId, "restitution_deadline", s.restitutionDeadline);
            ModHelper.SetModDataInt(ModId, "scavenger_visits", s.scavengerVisits);
            ModHelper.SetModDataBool(ModId, "revealed", s.revealed);
        }

        internal static bool Apply(StoryAction action)
        {
            var s = Load();
            if (!StoryCore.Apply(s, action, Day, Day)) return false;
            Save(s);
            return true;
        }

        internal static void OnWake()
        {
            OreDevice.TryDeliverReward();
            if (Load().stage == Stage.Restitution && Apply(StoryAction.ExpireRestitution))
                StoryEndings.ShowBadEnding();
        }

        internal static void OnLoaded()
        {
            OreDevice.TryDeliverReward();
            if (Load().stage == Stage.Restitution && Apply(StoryAction.ExpireRestitution))
            {
                StoryEndings.ShowBadEnding();
                return;
            }
            var stage = Load().stage;
            if (stage == Stage.Departed) StoryEndings.ShowSpaceEnding();
            else if (stage == Stage.BadEnding) StoryEndings.ShowBadEnding();
        }

        internal static void Queue(StoreClientManager manager)
        {
            if (manager == null || manager.clientStack == null) return;
            var state = Load();
            if (state.stage == Stage.Restitution && Apply(StoryAction.ExpireRestitution))
            {
                StoryEndings.ShowBadEnding();
                return;
            }
            if (!StoryCore.IsEligible(state, Day)) return;
            string id = state.stage == Stage.Judgment || state.stage == Stage.Restitution ? GodId : AdamId;
            if (manager.clientStack.Exists(c => c != null && (c.identifier == AdamId || c.identifier == GodId))) return;
            var customer = id == GodId ? MakeGod() : MakeAdam();
            if (customer != null) manager.AddClient(customer);
        }

        private static string AdamPortrait(StoryState s)
        {
            if (s.stage == Stage.Aid) return "adam_" + (s.refusals <= 0 ? "normal" : "hunger" + Math.Min(s.refusals, 3));
            return s.revealed ? "adam_scavenger" : "adam_normal";
        }

        private static StoreClient NewClient(string id, string name, string portrait)
        {
            return new StoreClient {
                identifier = id, sourceModId = ModId, displayName = name,
                spriteName = PortraitRegistry.Id(portrait), clientIntent = StoreClient.ClientIntent.DIALOGUE,
                dismissable = true, arrestable = false
            };
        }

        internal static Dialogue Chain(string name, string[] lines, Action onEnd = null)
        {
            var head = new Dialogue();
            var node = head;
            for (int i = 0; i < lines.Length; i++)
            {
                node.SetText(name, lines[i]);
                if (i + 1 < lines.Length) node = node.NextDialogue();
            }
            if (onEnd != null) node.SetEndAction(onEnd);
            return head;
        }

        internal static Dialogue Last(Dialogue root)
        {
            while (root.nextDialogue != null) root = root.nextDialogue;
            return root;
        }

        internal static void AddChoice(Dialogue node, string caption, Dialogue next, Action action = null)
        {
            // The vanilla choice bubble displays choiceNextDialogue.plainText,
            // not the choice item's name. Put the player's action there.
            var playerLine = new Dialogue().SetText(DialogUIManager.PLAYER, caption);
            playerLine.SetNextDialogue(next);
            node.AddChoice(DirectoryMaster.Item("$", true), caption, "", playerLine, action, null);
        }

        internal static void Dismiss()
        {
            if (PlayerStore.Instance != null) PlayerStore.Instance.DismissCurrentClient(false, true, true);
        }

        internal static Dialogue DismissLine(string name, string text) => new Dialogue().SetText(name, text).SetEndAction(Dismiss);

        private static void RevealName(StoreClient client)
        {
            if (Apply(StoryAction.FinishIntroduction)) client.displayName = "亞當";
        }

        private static void SetBarter(StoreClient client, Stage requested)
        {
            string requirement;
            switch (requested)
            {
                case Stage.Aid: requirement = "需要：食物 ×1 或 水 ×1"; client.clientBuyingTagList.Add("FOOD"); client.clientBuyingTagList.Add("WATER"); break;
                case Stage.Scavenger: requirement = "需要：普通電子元件 ×3"; break;
                case Stage.Battery: requirement = "需要：自充電能量電池 ×1"; break;
                case Stage.Restitution: requirement = "需要：食物合計至少 100 kcal"; client.clientBuyingTagList.Add("FOOD"); break;
                default: throw new ArgumentOutOfRangeException(nameof(requested));
            }
            var offer = new BarterOffer {
                barterTextBody = requirement,
                onInstance = () => { },
                acceptedResponse = "",
                deniedResponse = "",
                getBarterResult = () => new BarterResult {
                    isAccepted = CanAcceptBarter(client),
                    response = ""
                }
            };
            client.SetBarterOffer(offer);
        }

        internal static bool CanAcceptBarter(StoreClient client)
        {
            if (client == null || client.isBusinessComplete) return false;
            if (client.identifier != AdamId && client.identifier != GodId) return false;
            var candidate = Load();
            StoryAction action;
            switch (candidate.stage)
            {
                case Stage.Aid: action = StoryAction.DeliverAid; break;
                case Stage.Scavenger: action = StoryAction.DeliverElectronics; break;
                case Stage.Battery: action = StoryAction.DeliverBattery; break;
                case Stage.Restitution: action = StoryAction.DeliverRestitution; break;
                default: return false;
            }
            int day = StoreStation.GetDayCounter();
            return GameTradeAdapter.Matches(candidate.stage) && StoryCore.Apply(candidate, action, day, day);
        }

        private static Dialogue Decision(string name)
        {
            var dialogue = Chain(name, StoryText.Invitation);
            var end = Last(dialogue);
            var departure = Chain(name, StoryText.Departure, () => { if (Load().stage == Stage.Departed) StoryEndings.ShowSpaceEnding(); });
            AddChoice(end, "好，我跟你一起走。", departure, () => Apply(StoryAction.Leave));
            AddChoice(end, "謝謝，但我想留在這裡。", Chain(name, StoryText.Stay, Dismiss), () => {
                if (Apply(StoryAction.Stay)) OreDevice.ScheduleReward();
            });
            return dialogue;
        }

        private static void ConfigureAid(StoreClient client, StoryState state)
        {
            SetBarter(client, Stage.Aid);
            client.mainDialogue = Chain(client.displayName, StoryText.Hunger[Math.Min(Math.Max(state.refusals, 0), 3)]);
            var end = Last(client.mainDialogue);
            var trade = new Dialogue().SetText(client.displayName, "一份食物或一份水就足夠了。謝謝你願意看看。")
                .SetEndAction(() => client.barterOffer.InstanciateOffer());
            AddChoice(end, "我有食物或水，請收下。", trade);
            AddChoice(end, "抱歉，我不會給你。", DismissLine(client.displayName, "……我知道了。"), () => Apply(StoryAction.RefuseAid));
            AddChoice(end, "讓我再想想。", DismissLine(client.displayName, "我會再來問。"));
            client.acceptDealDialogue = Chain(client.displayName, StoryText.Introduction, () => RevealName(client));
            client.allDoneDialogue = DismissLine(client.displayName, "再次謝謝你。");
        }

        private static void ConfigureIntroduction(StoreClient client)
        {
            client.mainDialogue = Chain(client.displayName, StoryText.Introduction, () => {
                RevealName(client);
                Dismiss();
            });
        }

        private static void ConfigureScavenger(StoreClient client, StoryState state)
        {
            bool introduction = state.stage == Stage.Recovery;
            bool askForParts = !introduction && Day >= state.partsReadyDay && state.scavengerVisits % 2 == 1;
            if (askForParts)
            {
                SetBarter(client, Stage.Scavenger);
                client.mainDialogue = Chain(client.displayName, new[] { "電子零件的事，我又來問了。三個普通電子元件就足夠。", "沒準備好也沒關係，我會繼續撿廢料，過幾天再來。" }, () => client.barterOffer.InstanciateOffer());
                client.acceptDealDialogue = DismissLine(client.displayName, "太好了！我會先把它們帶去裝好。謝謝你。");
                client.allDoneDialogue = DismissLine(client.displayName, "我過幾天回來。");
            }
            else
            {
                client.clientIntent = StoreClient.ClientIntent.SELL;
                client.mainDialogue = Chain(client.displayName, introduction ? StoryText.Scavenger : StoryText.PartsWaiting, () => {
                    if (introduction) Apply(StoryAction.BeginScavenging);
                    if (PlayerStore.Instance != null)
                    {
                        PlayerStore.Instance.TryAddDirectSellingItemToTable("scrap_metal", false, false, false, 0);
                        PlayerStore.Instance.TryAddDirectSellingItemToTable("scrap_metal", false, false, false, 0);
                        PlayerStore.Instance.TryAddDirectSellingItemToTable("wire", false, false, false, 0);
                        client.OnIntroduced();
                    }
                });
                client.AddBasicDialog();
            }
            client.OnDismiss += () => { if (Load().stage == Stage.Scavenger) Apply(StoryAction.EndScrapVisit); };
        }

        internal static StoreClient MakeAdam()
        {
            var state = Load();
            var client = NewClient(AdamId, state.revealed ? "亞當" : "外星人", AdamPortrait(state));
            switch (state.stage)
            {
                case Stage.Aid: ConfigureAid(client, state); break;
                case Stage.Introduction: ConfigureIntroduction(client); break;
                case Stage.Recovery:
                case Stage.Scavenger: ConfigureScavenger(client, state); break;
                case Stage.Battery:
                    SetBarter(client, Stage.Battery);
                    client.mainDialogue = Chain(client.displayName, StoryText.BatteryRequest, () => client.barterOffer.InstanciateOffer());
                    client.acceptDealDialogue = Decision(client.displayName);
                    break;
                case Stage.Decision: client.mainDialogue = Decision(client.displayName); break;
                default: return null;
            }
            client.CompleteClientCreation(false);
            return client;
        }

        internal static StoreClient MakeGod()
        {
            var state = Load();
            var client = NewClient(GodId, "超級外星之神", "alien_god");
            SetBarter(client, Stage.Restitution);
            if (state.stage == Stage.Judgment)
            {
                client.mainDialogue = Chain(client.displayName, StoryText.Judgment);
                var node = Last(client.mainDialogue);
                AddChoice(node, StoryText.Apologize, Chain(client.displayName, StoryText.Restitution, () => client.barterOffer.InstanciateOffer()), () => Apply(StoryAction.Apologize));
                var bad = new Dialogue().SetText(client.displayName, "那麼，考驗到此為止。")
                    .SetEndAction(() => { if (Load().stage == Stage.BadEnding) StoryEndings.ShowBadEnding(); });
                AddChoice(node, "我不打算補救。", bad, () => Apply(StoryAction.RejectJudgment));
            }
            else if (state.stage == Stage.Restitution)
                client.mainDialogue = Chain(client.displayName, new[] { "期限還在。請上交至少 100 kcal 的食物。", "你選擇如何回應，我們會看見。" }, () => client.barterOffer.InstanciateOffer());
            else return null;
            client.acceptDealDialogue = Chain(client.displayName, StoryText.Redeemed, Dismiss);
            client.allDoneDialogue = DismissLine(client.displayName, "他恢復後會再來見你。");
            client.CompleteClientCreation(false);
            return client;
        }
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.AcceptBarter))]
    internal static class StoryBarterPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(PlayerStore __instance, ref int __state)
        {
            __state = -1;
            var client = __instance?.currentClientInstance?.GetClientBlueprint();
            if (client == null || (client.identifier != StoryRuntime.AdamId && client.identifier != StoryRuntime.GodId)) return true;
            if (!StoryRuntime.CanAcceptBarter(client)) return false;
            __state = (int)StoryRuntime.Load().stage;
            return true;
        }

        [HarmonyPostfix]
        private static void Postfix(int __state)
        {
            switch ((Stage)__state)
            {
                case Stage.Aid: StoryRuntime.Apply(StoryAction.DeliverAid); break;
                case Stage.Scavenger: StoryRuntime.Apply(StoryAction.DeliverElectronics); break;
                case Stage.Battery: StoryRuntime.Apply(StoryAction.DeliverBattery); break;
                case Stage.Restitution: StoryRuntime.Apply(StoryAction.DeliverRestitution); break;
            }
        }
    }
}
