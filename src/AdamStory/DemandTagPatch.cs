using System;
using HarmonyLib;

namespace AlienStory
{
    // Vanilla's negotiation tablet draws FOOD/WATER interest chips only for
    // buyers. Show those native chips for our barter visitor without changing
    // the actual trade intent outside this single synchronous UI build.
    [HarmonyPatch(typeof(NegociationUIManager), "BuildInfoBlock")]
    internal static class DemandTagPatch
    {
        internal struct State { internal bool changed; internal StoreClient.ClientIntent previous; }

        [HarmonyPrefix]
        private static void Prefix(StoreClient __0, ref State __state)
        {
            if (__0 == null || (__0.identifier != StoryRuntime.AdamId && __0.identifier != StoryRuntime.GodId) ||
                __0.clientIntent != StoreClient.ClientIntent.BARTER || __0.clientBuyingTagList == null || __0.clientBuyingTagList.Count == 0) return;
            __state.previous = __0.clientIntent;
            __state.changed = true;
            __0.clientIntent = StoreClient.ClientIntent.BUY;
        }

        [HarmonyPostfix]
        private static void Postfix(StoreClient __0, State __state) => Restore(__0, __state);

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, StoreClient __0, State __state)
        {
            Restore(__0, __state);
            return __exception;
        }

        private static void Restore(StoreClient client, State state)
        {
            if (client != null && state.changed) client.clientIntent = state.previous;
        }
    }
}
