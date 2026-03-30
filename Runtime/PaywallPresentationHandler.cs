using System;
using System.Collections.Generic;

namespace Superwall
{
    public class PaywallPresentationHandler
    {
        public Action<PaywallInfo> OnPresent;
        public Action<PaywallInfo, PaywallResult> OnDismiss;
        public Action<string> OnError;
        public Action<PaywallSkippedReason> OnSkip;
        public Func<CustomCallback, CustomCallbackResult> OnCustomCallback;
    }
}
