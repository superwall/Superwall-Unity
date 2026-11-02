using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Superwall
{
    public class PaywallPresentationHandler
    {
        public Action<PaywallInfo> OnPresent;
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
