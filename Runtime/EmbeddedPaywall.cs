using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Superwall.Internal;

namespace Superwall
{
    /// <summary>The callbacks of a paywall shown with <see cref="Superwall.ShowEmbeddedPaywall"/>.</summary>
    public class EmbeddedPaywallHandler
    {
        /// <summary>Called once the paywall is on screen.</summary>
        public Action<PaywallInfo> OnPresent;
        /// <summary>
        /// Called when no paywall is shown for the placement. The experiment is the one whose holdout group the user
        /// is in when the reason is <see cref="PaywallSkippedReason.Holdout"/>, and null otherwise.
        /// </summary>
        public Action<PaywallSkippedReason, Experiment> OnSkip;
        /// <summary>Called when the paywall couldn't be loaded or shown.</summary>
        public Action<string> OnError;
        /// <summary>
        /// Called when the user is done with the paywall: they bought, restored or closed it. The last argument says
        /// whether the paywall should go away, in which case it has been removed from the screen already. When it's
        /// false the paywall stays, for example after a purchase on a paywall that isn't set to dismiss.
        /// </summary>
        public Action<PaywallInfo, PaywallResult, bool> OnFinished;
        /// <summary>
        /// iOS only. Runs when the paywall requests a custom callback, as
        /// <see cref="PaywallPresentationHandler.OnCustomCallback"/> does. The Android SDK has no custom callbacks
        /// for embedded paywalls.
        /// </summary>
        public Func<CustomCallback, Task<CustomCallbackResult>> OnCustomCallback;
    }

    /// <summary>
    /// A paywall shown inside the game's screen instead of over it, as a native view laid over part of Unity's view.
    /// Get one from <see cref="Superwall.ShowEmbeddedPaywall"/>. It is drawn above everything Unity renders,
    /// including UI that overlaps its rectangle.
    /// </summary>
    public sealed class EmbeddedPaywall
    {
        private readonly string _viewId;

        internal EmbeddedPaywall(string viewId, string placement)
        {
            _viewId = viewId;
            Placement = placement;
        }

        public string Placement { get; }

        /// <summary>Whether the paywall has been taken off the screen, by <see cref="Remove"/> or because it finished or failed.</summary>
        public bool IsRemoved { get; internal set; }

        /// <summary>
        /// Moves or resizes the paywall. <paramref name="screenRect"/> is in Unity's screen space: pixels, with the
        /// origin at the bottom left, as <c>Screen.width</c> and <c>RectTransformUtility</c> use. The rectangle
        /// keeps its share of the screen when the screen rotates or is resized.
        /// </summary>
        public void SetRect(Rect screenRect)
        {
            if (IsRemoved) return;
            var normalized = Normalize(screenRect, Screen.width, Screen.height);
            Superwall.CallNative_SetEmbeddedPaywallRect(_viewId, normalized[0], normalized[1], normalized[2], normalized[3]);
        }

        /// <summary>Takes the paywall off the screen and lets go of it. Its handler isn't called again.</summary>
        public void Remove()
        {
            if (IsRemoved) return;
            IsRemoved = true;
            BridgeCallbackHandler.Instance?.UnregisterEmbeddedPaywall(_viewId);
            Superwall.CallNative_RemoveEmbeddedPaywall(_viewId);
        }

        /// <summary>
        /// A rectangle in Unity's screen space as fractions of the screen, measured from the top left, which is how
        /// both native bridges take it: Unity's pixel sizes needn't match the native view's.
        /// </summary>
        internal static float[] Normalize(Rect screenRect, float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0) return new float[] { 0, 0, 1, 1 };
            return new[]
            {
                screenRect.x / screenWidth,
                1f - (screenRect.y + screenRect.height) / screenHeight,
                screenRect.width / screenWidth,
                screenRect.height / screenHeight
            };
        }
    }
}
