using System;

namespace Superwall
{
    /// <summary>
    /// Receives the events of one Customer Center presentation. Pass it to
    /// <see cref="Superwall.PresentCustomerCenter"/>. Every method has a default implementation, so implement only
    /// the ones you need.
    /// </summary>
    public interface ICustomerCenterDelegate
    {
        /// <summary>
        /// iOS only. Called before purchases are restored: call <paramref name="proceed"/> once, with false to cancel,
        /// for example after the user declines to sign in. The restore waits until you do. On Android the Customer
        /// Center is its own activity, which pauses Unity, so this is never called there and the restore goes ahead.
        /// </summary>
        void ShouldRestorePurchases(Action<bool> proceed) => proceed(true);

        /// <summary>
        /// Called when the user taps a path, including custom and URL paths, before its action runs.
        /// <paramref name="purchase"/> is the purchase it applies to, or null for an action such as restore.
        /// </summary>
        void DidSelectAction(CustomerCenterAction action, string pathId, CustomerCenterPurchase purchase) { }

        /// <summary>Called when the user answers the survey of a path.</summary>
        void DidCompleteSurvey(string surveyId, string optionId, CustomerCenterAction action, string pathId) { }

        /// <summary>Called when a refund request finishes. See <see cref="CustomerCenterRefundStatus"/>.</summary>
        void DidCompleteRefundRequest(string productId, CustomerCenterRefundStatus status) { }

        /// <summary>Called when the Customer Center is dismissed.</summary>
        void DidDismiss() { }
    }
}
