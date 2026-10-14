using System.Collections.Generic;
using UnityEngine;
using Superwall;

namespace Superwall.Samples
{
    /// <summary>
    /// Example MonoBehaviour showing how to configure and use the Superwall SDK.
    /// Attach this to a GameObject in your first scene.
    /// </summary>
    public class SuperwallExample : MonoBehaviour, ISuperwallDelegate
    {
        [Header("Superwall Configuration")]
        [SerializeField] private string apiKey = "YOUR_API_KEY";

        [Tooltip("Logs Time.frameCount once a second, to check in logcat whether the Unity player keeps running while a paywall is on screen.")]
        [SerializeField] private bool logFrameHeartbeat;

        private float _nextHeartbeat;

        void Update()
        {
            if (!logFrameHeartbeat || Time.realtimeSinceStartup < _nextHeartbeat) return;
            _nextHeartbeat = Time.realtimeSinceStartup + 1f;
            Debug.Log($"[SuperwallExample] heartbeat frame={Time.frameCount}");
        }

        void Start()
        {
            // Configure Superwall with options
            var options = new SuperwallOptions
            {
                Logging = new Logging { Level = LogLevel.Debug }
            };

            Superwall.Configure(apiKey, options: options, completion: (success) =>
            {
                Debug.Log($"[SuperwallExample] Configured: {success}");
            });

            // Set this MonoBehaviour as the delegate
            Superwall.Shared.SetDelegate(this);
        }

        /// <summary>
        /// Call this to show a paywall for a placement.
        /// </summary>
        public void ShowPaywall(string placementName)
        {
            var handler = new PaywallPresentationHandler
            {
                OnPresent = (info) => Debug.Log($"[SuperwallExample] Paywall presented: {info.Identifier}"),
                OnDismiss = (info, result) => Debug.Log($"[SuperwallExample] Paywall dismissed: {result.Type}"),
                OnError = (error) => Debug.LogError($"[SuperwallExample] Paywall error: {error}"),
                OnSkip = (reason) => Debug.Log($"[SuperwallExample] Paywall skipped: {reason}")
            };

            Superwall.Shared.RegisterPlacement(placementName, handler: handler, feature: () =>
            {
                Debug.Log("[SuperwallExample] Feature unlocked!");
                // Your feature code here — this runs when the user has access
            });
        }

        /// <summary>
        /// EXPERIMENTAL (Android): shows the paywall in a Dialog over the Unity activity so Unity keeps
        /// running while it is on screen. Use with logFrameHeartbeat and a purchase controller to
        /// check that purchases complete.
        /// </summary>
        public void ShowPaywallInDialog(string placementName)
        {
            Superwall.Shared.PresentPaywallInDialogExperimental(placementName, completion: (result, error) =>
            {
                if (error != null)
                    Debug.LogError($"[SuperwallExample] Dialog paywall error: {error}");
                else
                    Debug.Log($"[SuperwallExample] Dialog paywall finished: {result.Type}");
            });
        }

        /// <summary>
        /// Identify a user after login.
        /// </summary>
        public void IdentifyUser(string userId)
        {
            Superwall.Shared.Identify(userId);
            Debug.Log($"[SuperwallExample] Identified user: {userId}");
        }

        /// <summary>
        /// Reset on logout.
        /// </summary>
        public void Logout()
        {
            Superwall.Shared.Reset();
            Debug.Log("[SuperwallExample] User reset");
        }

        // --- ISuperwallDelegate ---

        public void SubscriptionStatusDidChange(SubscriptionStatus from, SubscriptionStatus to)
        {
            Debug.Log($"[SuperwallExample] Subscription status changed: {from.Type} -> {to.Type}");
        }

        public void HandleSuperwallEvent(SuperwallEventInfo eventInfo)
        {
            Debug.Log($"[SuperwallExample] Event: {eventInfo.EventType}");
        }

        public void HandleCustomPaywallAction(string name)
        {
            Debug.Log($"[SuperwallExample] Custom action: {name}");
        }

        public void WillDismissPaywall(PaywallInfo paywallInfo) { }
        public void WillPresentPaywall(PaywallInfo paywallInfo) { }
        public void DidDismissPaywall(PaywallInfo paywallInfo) { }
        public void DidPresentPaywall(PaywallInfo paywallInfo) { }
        public void PaywallWillOpenURL(string url) => Application.OpenURL(url);
        public void PaywallWillOpenDeepLink(string url) => Application.OpenURL(url);

        public void HandleLog(string level, string scope, string message,
            Dictionary<string, object> info, string error)
        {
            if (level == "error")
                Debug.LogError($"[Superwall] {scope}: {message} {error}");
        }

        public void WillRedeemLink() { }
        public void DidRedeemLink(RedemptionResult result) { }

        public void HandleSuperwallDeepLink(string fullURL, List<string> pathComponents,
            Dictionary<string, string> queryParameters) { }

        public void CustomerInfoDidChange(CustomerInfo from, CustomerInfo to) { }
        public void UserAttributesDidChange(Dictionary<string, object> newAttributes) { }
    }
}
