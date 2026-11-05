using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienStory
{
    // Registration and preview are additive and never alter the main quest state.
    public sealed class AlienStoryMod : IMod
    {
        internal const string ModId = PortraitRegistry.ModId;
        internal const string AdamPreviewId = "personal.adam-story.preview.adam";
        internal const string GodPreviewId = "personal.adam-story.preview.god";
        internal const string Hunger1PreviewId = "personal.adam-story.preview.hunger1";
        internal const string Hunger2PreviewId = "personal.adam-story.preview.hunger2";
        internal const string Hunger3PreviewId = "personal.adam-story.preview.hunger3";
        internal const string ScavPreviewId = "personal.adam-story.preview.scavenger";
        internal const string QuickTestId = "personal.adam-story.preview.decision";
        private const string DogId = "personal.dog-customer.visitor";
        private const string PreviewKey = "preview_arrivals_v2";
        private const string QuickDueKey = "preview_decision_due";
        private const string QuickDoneKey = "preview_decision_done";
        private static ModConfig config;
        private static ConfigEntry<bool> previewEnabled;
        private readonly Dictionary<string, Func<StoreClient>> registered = new Dictionary<string, Func<StoreClient>>();

        public void Init(ModManifest manifest) { }

        public void OnEnable()
        {
            config = ModHelper.GetConfig(ModId);
            // Formal release starts without test visits, even if an older test
            // version left its preview setting enabled in the user's profile.
            previewEnabled = config.Define("run_test_mode", false, "角色展示", () => "全部角色狀態測試", () => "手動啟用後可展示七種狀態，不消耗物品；測試完成後自動關閉。", false);
            Register(AdamPreviewId, MakeAdamPreview);
            Register(Hunger1PreviewId, () => MakeHungerPreview(Hunger1PreviewId, 1, PreviewPlanner.Hunger1));
            Register(Hunger2PreviewId, () => MakeHungerPreview(Hunger2PreviewId, 2, PreviewPlanner.Hunger2));
            Register(Hunger3PreviewId, () => MakeHungerPreview(Hunger3PreviewId, 3, PreviewPlanner.Hunger3));
            Register(ScavPreviewId, MakeScavengerPreview);
            Register(GodPreviewId, MakeGodPreview);
            Register(QuickTestId, MakeQuickTest);
            Register(StoryRuntime.AdamId, StoryRuntime.MakeAdam);
            Register(StoryRuntime.GodId, StoryRuntime.MakeGod);
            ModHook.OnModItemDirectoryInit += OreDevice.Register;
            ModHook.OnGenerateCustomerVeryLate += QueuePreviews;
            ModHook.OnWakingUpLate += FinishPreviewAfterSave;
            ModHook.OnWakingUpLate += StoryRuntime.OnWake;
            ModHook.OnGameLoadedLate += StoryRuntime.OnLoaded;
        }

        public void OnDisable()
        {
            ModHook.OnGenerateCustomerVeryLate -= QueuePreviews;
            ModHook.OnWakingUpLate -= FinishPreviewAfterSave;
            ModHook.OnWakingUpLate -= StoryRuntime.OnWake;
            ModHook.OnModItemDirectoryInit -= OreDevice.Register;
            ModHook.OnGameLoadedLate -= StoryRuntime.OnLoaded;
            foreach (var pair in registered)
                if (StoreClientListDict.storeClientDict.TryGetValue(pair.Key, out var factory) && factory == pair.Value)
                    StoreClientListDict.storeClientDict.Remove(pair.Key);
            registered.Clear();
            PortraitRegistry.Clear();
        }

        private void Register(string id, Func<StoreClient> factory)
        {
            if (StoreClientListDict.storeClientDict.ContainsKey(id)) throw new InvalidOperationException("Client ID already registered: " + id);
            StoreClientListDict.storeClientDict.Add(id, factory);
            registered.Add(id, factory);
        }

        private static int Seen => ModHelper.GetModDataInt(ModId, PreviewKey, 0);
        private static void MarkArrived(int role)
        {
            int seen = PreviewPlanner.Arrived(Seen, role);
            ModHelper.SetModDataInt(ModId, PreviewKey, seen);
            if (PreviewPlanner.IsComplete(seen) && ModHelper.GetModDataInt(ModId, QuickDueKey, -1) < 0)
            {
                ModHelper.SetModDataInt(ModId, QuickDueKey, StoreStation.GetDayCounter() + 3);
                ModHelper.SetModDataBool(ModId, QuickDoneKey, false);
            }
        }

        private static void FinishPreviewAfterSave()
        {
            if (PreviewPlanner.IsComplete(Seen) && ModHelper.GetModDataBool(ModId, QuickDoneKey, false) && previewEnabled != null && previewEnabled.Value)
            {
                previewEnabled.Set(false);
                config.Save();
            }
        }

        private static void QueuePreviews(StoreClientManager manager)
        {
            if (manager == null || manager.clientStack == null) return;
            if (previewEnabled == null || !previewEnabled.Value) { StoryRuntime.Queue(manager); return; }
            int missing = PreviewPlanner.Missing(Seen);
            if (missing == 0) { QueueQuickTest(manager); StoryRuntime.Queue(manager); return; }
            if ((missing & PreviewPlanner.Dog) != 0)
            {
                var dog = manager.clientStack.Find(c => c != null && c.identifier == DogId);
                if (dog == null && StoreClientListDict.storeClientDict.TryGetValue(DogId, out var factory))
                {
                    dog = factory();
                    if (dog != null) manager.AddClient(dog);
                }
                if (dog != null)
                {
                    dog.displayName = "狗狗（測試展示）";
                    dog.clientIntent = StoreClient.ClientIntent.DIALOGUE;
                    dog.mainDialogue = new Dialogue().SetText(dog.displayName, "旺旺～").SetEndAction(StoryRuntime.Dismiss);
                    dog.OnArrival += () => {
                        var dict = SpriteDict.Instance;
                        if (dict != null && dict.spriteDictionary != null && dog.spriteName != null && dict.spriteDictionary.TryGetValue(dog.spriteName, out var picture) && picture != null)
                            MarkArrived(PreviewPlanner.Dog);
                        else Debug.LogWarning("[AlienStory] Dog preview portrait missing; it will be retried.");
                    };
                }
                else Debug.LogWarning("[AlienStory] Dog mod is not active; its preview will be retried.");
            }
            if ((missing & PreviewPlanner.Adam) != 0 && !manager.clientStack.Exists(c => c != null && c.identifier == AdamPreviewId))
                manager.AddClient(MakeAdamPreview());
            if ((missing & PreviewPlanner.Hunger1) != 0 && !manager.clientStack.Exists(c => c != null && c.identifier == Hunger1PreviewId))
                manager.AddClient(MakeHungerPreview(Hunger1PreviewId, 1, PreviewPlanner.Hunger1));
            if ((missing & PreviewPlanner.Hunger2) != 0 && !manager.clientStack.Exists(c => c != null && c.identifier == Hunger2PreviewId))
                manager.AddClient(MakeHungerPreview(Hunger2PreviewId, 2, PreviewPlanner.Hunger2));
            if ((missing & PreviewPlanner.Hunger3) != 0 && !manager.clientStack.Exists(c => c != null && c.identifier == Hunger3PreviewId))
                manager.AddClient(MakeHungerPreview(Hunger3PreviewId, 3, PreviewPlanner.Hunger3));
            if ((missing & PreviewPlanner.Scavenger) != 0 && !manager.clientStack.Exists(c => c != null && c.identifier == ScavPreviewId))
                manager.AddClient(MakeScavengerPreview());
            if ((missing & PreviewPlanner.God) != 0 && !manager.clientStack.Exists(c => c != null && c.identifier == GodPreviewId))
                manager.AddClient(MakeGodPreview());
        }

        private static StoreClient MakeAdamPreview() => MakeHungerPreview(AdamPreviewId, 0, PreviewPlanner.Adam);

        private static void QueueQuickTest(StoreClientManager manager)
        {
            int due = ModHelper.GetModDataInt(ModId, QuickDueKey, -1);
            if (due <= 0 || StoreStation.GetDayCounter() < due || ModHelper.GetModDataBool(ModId, QuickDoneKey, false)) return;
            if (!manager.clientStack.Exists(c => c != null && c.identifier == QuickTestId)) manager.AddClient(MakeQuickTest());
        }

        private static Dialogue TestDecision(string name, bool markQuickDone)
        {
            var dialogue = StoryRuntime.Chain(name, StoryText.Invitation);
            var last = StoryRuntime.Last(dialogue);
            Action mark = () => { if (markQuickDone) ModHelper.SetModDataBool(ModId, QuickDoneKey, true); };
            var space = StoryRuntime.Chain(name, StoryText.Departure,
                () => StoryEndings.ShowTest("太空結局", "你與亞當飛向星海。", "ending_space"));
            var stay = StoryRuntime.Chain(name, StoryText.Stay,
                () => StoryEndings.ShowTest("留下的禮物", "正式劇情中會獲得每三天產出十個金屬礦石的裝置。", "ore_device"));
            StoryRuntime.AddChoice(last, "好，我跟你一起走。", space, mark);
            StoryRuntime.AddChoice(last, "謝謝，但我想留在這裡。", stay, mark);
            return dialogue;
        }

        private static StoreClient MakeQuickTest()
        {
            var client = NewPreview(QuickTestId, "亞當（測試）", "adam_scavenger", 0);
            client.mainDialogue = TestDecision(client.displayName, true);
            client.CompleteClientCreation(false);
            return client;
        }

        private static StoreClient MakeHungerPreview(string id, int level, int role)
        {
            string asset = level == 0 ? "adam_normal" : "adam_hunger" + level;
            var client = NewPreview(id, "外星訪客（測試 " + (level + 1) + "）", asset, role);
            client.mainDialogue = StoryRuntime.Chain(client.displayName, StoryText.Hunger[level]);
            var last = StoryRuntime.Last(client.mainDialogue);
            StoryRuntime.AddChoice(last, "我有食物或水，請收下。", StoryRuntime.Chain(client.displayName, StoryText.Introduction, StoryRuntime.Dismiss));
            StoryRuntime.AddChoice(last, "抱歉，我不會給你。", StoryRuntime.DismissLine(client.displayName, "……我知道了。"));
            StoryRuntime.AddChoice(last, "讓我再想想。", StoryRuntime.DismissLine(client.displayName, "我會再來問。"));
            client.CompleteClientCreation(false);
            return client;
        }

        private static StoreClient MakeScavengerPreview()
        {
            var client = NewPreview(ScavPreviewId, "亞當（拾荒測試）", "adam_scavenger", PreviewPlanner.Scavenger);
            client.mainDialogue = StoryRuntime.Chain(client.displayName, StoryText.Scavenger);
            var battery = StoryRuntime.Chain(client.displayName, StoryText.BatteryRequest);
            StoryRuntime.AddChoice(StoryRuntime.Last(battery), "電池準備好了。", TestDecision(client.displayName, false));
            StoryRuntime.AddChoice(StoryRuntime.Last(battery), "我還需要一點時間。", StoryRuntime.DismissLine(client.displayName, "我會再來。"));
            StoryRuntime.AddChoice(StoryRuntime.Last(client.mainDialogue), "零件準備好了。", battery);
            StoryRuntime.AddChoice(StoryRuntime.Last(client.mainDialogue), "我還需要一點時間。", StoryRuntime.DismissLine(client.displayName, "我會再來。"));
            client.CompleteClientCreation(false);
            return client;
        }

        private static StoreClient MakeGodPreview()
        {
            var client = NewPreview(GodPreviewId, "外星之神（測試）", "alien_god", PreviewPlanner.God);
            client.mainDialogue = StoryRuntime.Chain(client.displayName, StoryText.Judgment);
            var restitution = StoryRuntime.Chain(client.displayName, StoryText.Restitution);
            StoryRuntime.AddChoice(StoryRuntime.Last(restitution), "我把食物準備好了。", StoryRuntime.Chain(client.displayName, StoryText.Redeemed, StoryRuntime.Dismiss));
            var bad = new Dialogue().SetText(client.displayName, "這次先試看壞結局。")
                .SetEndAction(() => StoryEndings.ShowTest("壞結局", "星空亮起藍光，考驗以失敗告終。", "ending_bad"));
            StoryRuntime.AddChoice(StoryRuntime.Last(restitution), "我沒能在期限內備好。", bad);
            StoryRuntime.AddChoice(StoryRuntime.Last(client.mainDialogue), StoryText.Apologize, restitution);
            StoryRuntime.AddChoice(StoryRuntime.Last(client.mainDialogue), "我不打算補救。", bad);
            client.CompleteClientCreation(false);
            return client;
        }

        private static StoreClient NewPreview(string id, string name, string asset, int role)
        {
            var client = new StoreClient {
                identifier = id, sourceModId = ModId, displayName = name,
                spriteName = PortraitRegistry.Id(asset), clientIntent = StoreClient.ClientIntent.DIALOGUE,
                dismissable = true, arrestable = false
            };
            if (role != 0) client.OnArrival += () => {
                if (PortraitRegistry.Bind(SpriteDict.Instance, client.spriteName)) MarkArrived(role);
                else Debug.LogWarning("[AlienStory] Preview portrait missing; it will be retried: " + client.spriteName);
            };
            return client;
        }
    }
}
