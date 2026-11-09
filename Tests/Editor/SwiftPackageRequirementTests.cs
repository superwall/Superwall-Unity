using NUnit.Framework;
using Superwall.Editor;

namespace Superwall.Editor.Tests
{
    class SwiftPackageRequirementTests
    {
        // An XCRemoteSwiftPackageReference as it appears in project.pbxproj.
        private static string Pbxproj(string url, string minimumVersion) =>
            "\t\tA1B2C3 /* XCRemoteSwiftPackageReference */ = {\n" +
            "\t\t\tisa = XCRemoteSwiftPackageReference;\n" +
            "\t\t\trepositoryURL = \"" + url + "\";\n" +
            "\t\t\trequirement = {\n" +
            "\t\t\t\tkind = upToNextMajorVersion;\n" +
            "\t\t\t\tminimumVersion = " + minimumVersion + ";\n" +
            "\t\t\t};\n" +
            "\t\t};\n";

        private static string Podfile(string pods) =>
            "platform :ios, '15.0'\nuse_frameworks!\n\ntarget 'UnityFramework' do\n" + pods + "end\n";

        [Test]
        public void SuperwallKitMinimumVersion_IncludesTheFixThatKeepsPaywallsToTheGamesOrientations()
        {
            var minimum = System.Version.Parse(SuperwallPostBuildProcessor.SuperwallKitMinimumVersion);

            Assert.GreaterOrEqual(minimum, new System.Version(4, 19, 1));
            Assert.Less(minimum, new System.Version(5, 0, 0));
        }

        [Test]
        public void ContainsSuperwallKitPackage_FindsTheReference()
        {
            Assert.IsTrue(SuperwallPostBuildProcessor.ContainsSuperwallKitPackage(
                Pbxproj(SuperwallPostBuildProcessor.SuperwallKitPackageUrl, "4.19.1")));
            Assert.IsFalse(SuperwallPostBuildProcessor.ContainsSuperwallKitPackage(
                Pbxproj("https://github.com/apple/swift-system.git", "1.0.0")));
        }

        [TestCase("4.12.10")]
        [TestCase("\"4.0.0\"")]
        public void RaiseSuperwallKitPackageRequirement_RaisesAnOlderMinimum(string minimum)
        {
            string url = SuperwallPostBuildProcessor.SuperwallKitPackageUrl;

            Assert.AreEqual(
                Pbxproj(url, SuperwallPostBuildProcessor.SuperwallKitMinimumVersion),
                SuperwallPostBuildProcessor.RaiseSuperwallKitPackageRequirement(Pbxproj(url, minimum)));
        }

        [TestCase("4.19.1")]
        [TestCase("9.0.0")]
        public void RaiseSuperwallKitPackageRequirement_KeepsTheCurrentOrANewerMinimum(string minimum)
        {
            string pbxproj = Pbxproj(SuperwallPostBuildProcessor.SuperwallKitPackageUrl, minimum);

            Assert.AreEqual(pbxproj, SuperwallPostBuildProcessor.RaiseSuperwallKitPackageRequirement(pbxproj));
        }

        [Test]
        public void RaiseSuperwallKitPackageRequirement_LeavesOtherPackagesAlone()
        {
            string pbxproj = Pbxproj("https://github.com/apple/swift-system.git", "1.0.0");

            Assert.AreEqual(pbxproj, SuperwallPostBuildProcessor.RaiseSuperwallKitPackageRequirement(pbxproj));
        }

        [TestCase("  pod 'SuperwallKit', '~> 4.0'\n")]
        [TestCase("  pod 'SuperwallKit', '>= 4.19.1', '< 5.0'\n")]
        [TestCase("  pod \"SuperwallKit\", \"4.19.3\"\n")]
        public void RemoveSuperwallKitPod_RemovesThePodAndKeepsOthers(string superwallPod)
        {
            string otherPod = "  pod 'Firebase/Analytics'\n";

            Assert.AreEqual(
                Podfile(otherPod),
                SuperwallPostBuildProcessor.RemoveSuperwallKitPod(Podfile(superwallPod + otherPod)));
        }

        [Test]
        public void RemoveSuperwallKitPod_LeavesAPodfileWithoutItAlone()
        {
            string podfile = Podfile("  pod 'Firebase/Analytics'\n");

            Assert.AreEqual(podfile, SuperwallPostBuildProcessor.RemoveSuperwallKitPod(podfile));
        }
    }
}
