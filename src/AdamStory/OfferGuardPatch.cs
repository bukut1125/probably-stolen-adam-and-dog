using HarmonyLib;

namespace AlienStory
{
    // The vanilla caller starts success dialogue after invoking AcceptBarter.
    // Guard its entry as well as the transfer so invalid requests cannot show
    // a success line if the transfer is refused by our lower-level guard.
    [HarmonyPatch(typeof(NegociationUIManager), "OnOfferAcceptClick")]
    internal static class OfferGuardPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            var client = PlayerStore.Instance?.currentClientInstance?.GetClientBlueprint();
            if (client == null || (client.identifier != StoryRuntime.AdamId && client.identifier != StoryRuntime.GodId)) return true;
            return client.clientIntent != StoreClient.ClientIntent.BARTER || StoryRuntime.CanAcceptBarter(client);
        }
    }
}
