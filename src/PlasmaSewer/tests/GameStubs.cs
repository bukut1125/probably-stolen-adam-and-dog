using System;
using System.Collections.Generic;
using System.Linq;
namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch:Attribute { public HarmonyPatch(Type t,string n){} }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPrefix:Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPostfix:Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyFinalizer:Attribute { }
}
namespace UnityEngine { public class Object { } public class GameObject { public bool activeSelf; } public static class Debug { public static void LogWarning(object x){} } }
public interface IMod { void Init(ModManifest m); void OnEnable(); void OnDisable(); }
public class ModManifest { }
public class TagState { public int val; public int GetInt()=>val; public TagState SetInt(int value){val=value;return this;} }
public class GameItem {
    public string identifier; public Dictionary<string,TagState> tags=new(); public HashSet<string> flags=new();
    public bool IsTag(string k)=>flags.Contains(k); public bool EnableTag(string k,bool skip){flags.Add(k);return true;} public bool DisableTag(string k,bool skip){flags.Remove(k);return true;} public TagState GetTagReadonly(string k)=>tags.TryGetValue(k,out var t)?t:new();
    public GameItem ModifyTag(string k,Action<TagState> f,bool skip){if(!tags.ContainsKey(k))tags[k]=new();f(tags[k]);return this;}
    public void Target(GameItem target) { }
}
public class Liquid {
    public string identifier,part,currentPart,displayName,color; public int colorMultiplier,particleSize,baseMaxPurityFromMachine,basePurifierRemoveAmount; public double lethality; public bool canBeRemovedByPurifier;
    public const string LIQUID_WATER="water", LIQUID_ZYANIDE="zyanide"; public static List<Liquid> Liquids=new(); public static Dictionary<string,Liquid> LiquidDict=new();
    public static string GetCurrentPartFromId(string id)=>LiquidDict[id].currentPart;
}
public static class WaterHelper {
    public const string LIQUID_CONTAINER_TAG="water_container";
    public static int GetTotalVolume(GameItem item)=>Liquid.Liquids.Sum(l=>item.GetTagReadonly(l.currentPart).GetInt());
    public static bool IsFaucet(GameItem item)=>item.identifier=="faucet";
    public static void TransferLiquid(GameItem sourceContainer,GameItem targetContainer){}
    public static string GetLiquidContainerColor(GameItem item)=>"000000";
    public static GameItem Filter;
    public static GameItem GetFilterItem(GameItem item)=>Filter;
    public static int HandleFilter(string id,int amount,GameItem source,GameItem filter)=>amount/2;
    public static void AddLiquid(GameItem item,string id,int amount){string k=Liquid.GetCurrentPartFromId(id);int v=item.GetTagReadonly(k).GetInt();item.ModifyTag(k,t=>t.SetInt(v+amount),false);}
}
public static class WaterFeatureHelper { public static void UpdateWaterFeature(GameItem x,bool b){} }
public static class SpriteHelper { public static void UpdateWaterContainerSprite(GameItem x){} }
public class StoreStation { public static StoreStation Instance=new(); public static int Day; public static int GetDayCounter()=>Day; }
public class PlayerStore { public static PlayerStore Instance=new(); public string runID="test-run"; public int saveSlotId=1; public Dictionary<string,NetworkUpgrade> networkUpgrade=new(); }
public class NetworkUpgrade { public static bool Gutter,Rust; public static bool IsUnlocked(string id)=>id=="GUTTERFLOW_TAP"?Gutter:id=="RUSTWATER_TAP"&&Rust; }
public class EmporiumEntry { public void ApplyFaucetState(bool active){} }
public class ConfigEntry<T> { public T Value; }
public class ModConfig { public ConfigEntry<bool> Entry=new(); public ConfigEntry<bool> Define(string key,bool value,string group,Func<string> label,Func<string> description,bool restart){Entry.Value=value;return Entry;} }
public static class ModHelper { public static ModConfig Config=new(); public static ModConfig GetConfig(string id)=>Config; }
public static class ModHook {
    public static event Action OnGameLoadedInit,OnGameLoadedLate,OnWakingUpLate;
    public static event Action<string> OnModConfigApplied,OnNetworkUpgradeUnlocked;
    public static event Action<RichTextBuilder,GameItem> OnCreateTooltipLate;
    public static void LoadInit()=>OnGameLoadedInit?.Invoke(); public static void LoadLate()=>OnGameLoadedLate?.Invoke(); public static void Wake()=>OnWakingUpLate?.Invoke();
    public static void ConfigChanged(string id)=>OnModConfigApplied?.Invoke(id); public static void Upgrade(string id)=>OnNetworkUpgradeUnlocked?.Invoke(id);
    public static void Tooltip(RichTextBuilder b,GameItem i)=>OnCreateTooltipLate?.Invoke(b,i);
}
public class StoreEvent {
    public enum EventType { NORMALE, THREAT, COSMETIC, INNATE } public enum EventArea { UPPER, LOWER, ALL }
    public string identifier,newsName,displayName,newsDescription; public EventType eventType; public EventArea eventArea; public int duration; public bool isPermanent,isHiddenFromNewsPaper;
}
public class StoreEventManager { public List<StoreEvent> GetActiveEvents()=>new(); }
public class NewsUIManager { public UnityEngine.GameObject uiGameObject=new(); public void PopulateUI(){} public void ToggleUI(){} }
public class StoreUIManager { public static StoreUIManager Instance=new(); public int Count; public void Notify(string a,string b){Count++;} }
public class RichTextBuilder { public List<string> Lines=new(); public RichTextBuilder AddLine(string text, bool ignoreEmpty=true,RenderHandler.ColorPalette color=RenderHandler.ColorPalette.None){Lines.Add(text);return this;} }
public static class RenderHandler { public enum ColorPalette { None,Red } }
namespace PlasmaSewer {
 internal static class PlasmaVisual { public static bool Visible; internal static void SetVisible(bool v){Visible=v;} internal static void Hide(){Visible=false;} }
 internal static class NewsPager {
  public static int PageIndex {get; private set;} public static int PageCount=1; public static bool Allow=true,Projected; public static int RefreshCalls;
  public static void ResetPage(){PageIndex=0;} public static void BeginPopulate(){Projected=false;PageCount=1;} public static void SetProjection(int selected,int count){PageIndex=selected;PageCount=count;Projected=true;}
  public static bool TryEnsure(NewsUIManager manager)=>Allow && manager!=null; public static void Refresh(NewsUIManager manager){RefreshCalls++;} public static void Destroy(){}
 }
}
