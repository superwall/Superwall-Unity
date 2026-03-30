using System;

namespace Superwall
{
    public interface IPurchaseController
    {
        void PurchaseFromAppStore(string productId, Action<PurchaseResult> completion);
        void PurchaseFromGooglePlay(string productId, string basePlanId, string offerId, Action<PurchaseResult> completion);
        void RestorePurchases(Action<RestorationResult> completion);
    }
}
