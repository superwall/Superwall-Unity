using System.Collections.Generic;

namespace Superwall
{
    public interface ISuperwallDelegate
    {
        void SubscriptionStatusDidChange(SubscriptionStatus from, SubscriptionStatus to);
        void HandleSuperwallEvent(SuperwallEventInfo eventInfo);
        void HandleCustomPaywallAction(string name);
        void WillDismissPaywall(PaywallInfo paywallInfo);
        void WillPresentPaywall(PaywallInfo paywallInfo);
        void DidDismissPaywall(PaywallInfo paywallInfo);
        void DidPresentPaywall(PaywallInfo paywallInfo);
        void PaywallWillOpenURL(string url);
        void PaywallWillOpenDeepLink(string url);
        void HandleLog(string level, string scope, string message, Dictionary<string, object> info, string error);
        void WillRedeemLink();
        void DidRedeemLink(RedemptionResult result);
        void HandleSuperwallDeepLink(string fullURL, List<string> pathComponents, Dictionary<string, string> queryParameters);
        void CustomerInfoDidChange(CustomerInfo from, CustomerInfo to);
        void UserAttributesDidChange(Dictionary<string, object> newAttributes);
    }
}
