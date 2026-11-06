using NUnit.Framework;
using Superwall.Editor;

namespace Superwall.Editor.Tests
{
    class PodfileRequirementTests
    {
        private static string Podfile(string pod) =>
            "platform :ios, '15.0'\nuse_frameworks!\n\ntarget 'UnityFramework' do\n  " + pod + "\nend\n";

        [Test]
        public void SuperwallKitPod_RequiresTheVersionThatKeepsPaywallsToTheGamesOrientations()
        {
            Assert.AreEqual("pod 'SuperwallKit', '>= 4.19.1', '< 5.0'", SuperwallPostBuildProcessor.SuperwallKitPod);
        }

        [TestCase("pod 'SuperwallKit', '~> 4.0'")]
        [TestCase("pod 'SuperwallKit', '>= 4.12.10', '< 5.0'")]
        public void UpdateSuperwallKitRequirement_MovesRequirementsThisPackageWroteBefore(string previousPod)
        {
            string updated = SuperwallPostBuildProcessor.UpdateSuperwallKitRequirement(Podfile(previousPod));

            Assert.AreEqual(Podfile(SuperwallPostBuildProcessor.SuperwallKitPod), updated);
        }

        [Test]
        public void UpdateSuperwallKitRequirement_LeavesTheCurrentRequirementAlone()
        {
            string podfile = Podfile(SuperwallPostBuildProcessor.SuperwallKitPod);

            Assert.AreEqual(podfile, SuperwallPostBuildProcessor.UpdateSuperwallKitRequirement(podfile));
        }

        [Test]
        public void UpdateSuperwallKitRequirement_KeepsARequirementSetByHand()
        {
            string podfile = Podfile("pod 'SuperwallKit', '4.19.3'");

            Assert.AreEqual(podfile, SuperwallPostBuildProcessor.UpdateSuperwallKitRequirement(podfile));
        }
    }
}
