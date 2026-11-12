using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Superwall
{
    public class PaywallPresentationHandler
    {
        public Action<PaywallInfo> OnPresent;
        /// <summary>
        /// iOS only. Runs when the paywall is about to be dismissed, before <see cref="OnDismiss"/>. Never called on
        /// Android, whose SDK has no such callback.
        /// </summary>
        public Action<PaywallInfo, PaywallResult> OnWillDismiss;
        public Action<PaywallInfo, PaywallResult> OnDismiss;
        public Action<string> OnError;
        public Action<PaywallSkippedReason> OnSkip;

        /// <summary>
        /// Runs when the paywall requests a custom callback, for work such as creating an account
        /// through your own API. The result you return decides which branch the paywall takes
        /// (onSuccess or onFailure), and its Data is readable there as callbacks.NAME.data.KEY.
        /// A blocking callback holds the paywall's tap action until this task completes, so make
        /// sure it finishes. If it throws, is cancelled or returns null, the paywall gets a failure.
        /// </summary>
        public Func<CustomCallback, Task<CustomCallbackResult>> OnCustomCallback;
    }
}
