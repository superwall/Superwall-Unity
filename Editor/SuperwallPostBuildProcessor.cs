using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using System.IO;
using System.Text.RegularExpressions;

namespace Superwall.Editor
{
    public class SuperwallPostBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 99;

        public const string SuperwallKitPackageUrl = "https://github.com/superwall/Superwall-iOS";
        public const string SuperwallKitProduct = "SuperwallKit";

        // The exported Xcode project requires SuperwallKit from this version up to the next major.
        // 4.12.10 added the custom callback API the bridge calls. 4.19.1 keeps a paywall to the
        // orientations the game allows (Screen.autorotateTo*), and stops it leaving the game
        // rotated after it closes. native-sdk-bump.yml rewrites this line.
        public const string SuperwallKitMinimumVersion = "4.19.1";

        // SuperwallKit 4.18.0 raised its deployment target from iOS 13 to iOS 15.
        private const string MinimumIOSVersion = "15.0";

        // A SuperwallKit pod line, whatever its requirement. Versions before 0.6.0 got SuperwallKit
        // from CocoaPods; linking it from the pod and the Swift package would duplicate every class.
        private static readonly Regex SuperwallKitPodLine =
            new Regex(@"^[ \t]*pod[ \t]+['""]SuperwallKit['""][^\n]*\n?", RegexOptions.Multiline);

        // The minimumVersion of the XCRemoteSwiftPackageReference for SuperwallKit. pbxproj keys
        // are sorted, so repositoryURL comes before requirement, and kind before minimumVersion.
        private static readonly Regex SuperwallKitPackageMinimumVersion = new Regex(
            @"(repositoryURL\s*=\s*""?https://github\.com/superwall/Superwall-iOS(?:\.git)?/?""?;\s*requirement\s*=\s*\{[^}]*?minimumVersion\s*=\s*)""?([^;""\s]+)""?;",
            RegexOptions.IgnoreCase);

        /// <summary>
        /// Whether the Xcode project (the text of project.pbxproj) already references the
        /// SuperwallKit Swift package, as it does after an Append build.
        /// </summary>
        public static bool ContainsSuperwallKitPackage(string pbxprojContent)
        {
            return pbxprojContent.IndexOf("github.com/superwall/Superwall-iOS",
                System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Raises the SuperwallKit package's minimum version in project.pbxproj to
        /// SuperwallKitMinimumVersion. A newer minimum, set by hand, is kept. Returns the content
        /// unchanged if it has no SuperwallKit package with a minimum version.
        /// </summary>
        public static string RaiseSuperwallKitPackageRequirement(string pbxprojContent)
        {
            var required = System.Version.Parse(SuperwallKitMinimumVersion);
            return SuperwallKitPackageMinimumVersion.Replace(pbxprojContent, match =>
            {
                if (System.Version.TryParse(match.Groups[2].Value, out var current) && current >= required)
                {
                    return match.Value;
                }
                return match.Groups[1].Value + SuperwallKitMinimumVersion + ";";
            });
        }

        /// <summary>
        /// Removes every SuperwallKit pod line from a Podfile. Returns the Podfile unchanged if it
        /// has none.
        /// </summary>
        public static string RemoveSuperwallKitPod(string podfileContent)
        {
            return SuperwallKitPodLine.Replace(podfileContent, "");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform == BuildTarget.iOS)
            {
                PostProcessIOS(report.summary.outputPath);
            }
#endif
        }

#if UNITY_IOS
        private static void PostProcessIOS(string buildPath)
        {
            // SuperwallKit 4.18+ requires iOS 15. Xcode refuses to link the package into a target
            // with a lower deployment target (PlayerSettings → Other Settings → Target minimum iOS Version).
            string iosTarget = PlayerSettings.iOS.targetOSVersionString;
            if (!string.IsNullOrEmpty(iosTarget)
                && System.Version.TryParse(iosTarget.Contains(".") ? iosTarget : iosTarget + ".0", out var target)
                && target < System.Version.Parse(MinimumIOSVersion))
            {
                Debug.LogWarning($"[Superwall] SuperwallKit requires iOS {MinimumIOSVersion} or later, but Target minimum iOS Version is {iosTarget}. Raise it in Player Settings → Other Settings, or the Xcode build will fail to link SuperwallKit.");
            }

            RemovePodFromAppendedBuild(buildPath);

            string projPath = PBXProject.GetPBXProjectPath(buildPath);
            bool hasPackage = ContainsSuperwallKitPackage(File.ReadAllText(projPath));

            var project = new PBXProject();
            project.ReadFromFile(projPath);

            string unityFrameworkGuid = project.GetUnityFrameworkTargetGuid();
            string mainTargetGuid = project.GetUnityMainTargetGuid();

            // An Append build keeps the package reference from the last export; adding it again
            // would duplicate it.
            if (!hasPackage)
            {
                string packageGuid = project.AddRemotePackageReferenceAtVersionUpToNextMajor(
                    SuperwallKitPackageUrl, SuperwallKitMinimumVersion);
                project.AddRemotePackageFrameworkToProject(unityFrameworkGuid, SuperwallKitProduct, packageGuid, false);
                Debug.Log($"[Superwall] Added the SuperwallKit Swift package (from {SuperwallKitMinimumVersion} up to the next major version) to UnityFramework");
            }

            // Enable modules and Swift for the framework target
            project.SetBuildProperty(unityFrameworkGuid, "CLANG_ENABLE_MODULES", "YES");
            project.SetBuildProperty(unityFrameworkGuid, "SWIFT_VERSION", "5.0");

            // Set supported platforms to include simulator
            foreach (var targetGuid in new[] { mainTargetGuid, unityFrameworkGuid })
            {
                project.SetBuildProperty(targetGuid, "SUPPORTED_PLATFORMS", "iphonesimulator iphoneos");
                project.SetBuildProperty(targetGuid, "SUPPORTS_MACCATALYST", "NO");
            }

            project.WriteToFile(projPath);

            if (hasPackage)
            {
                // Without this, the kept requirement lets Package.resolved hold a SuperwallKit
                // older than the bridge needs.
                string content = File.ReadAllText(projPath);
                string updated = RaiseSuperwallKitPackageRequirement(content);
                if (updated != content)
                {
                    File.WriteAllText(projPath, updated);
                    Debug.Log($"[Superwall] Raised the SuperwallKit package requirement to {SuperwallKitMinimumVersion}");
                }
            }
        }

        // An Append build over an export from a version that used CocoaPods keeps its Podfile and
        // Pods integration. Drop SuperwallKit from it so it is not linked twice.
        private static void RemovePodFromAppendedBuild(string buildPath)
        {
            string podfilePath = Path.Combine(buildPath, "Podfile");
            if (!File.Exists(podfilePath)) return;

            string podfile = File.ReadAllText(podfilePath);
            string updated = RemoveSuperwallKitPod(podfile);
            if (updated == podfile) return;

            File.WriteAllText(podfilePath, updated);
            Debug.LogWarning(
                "[Superwall] SuperwallKit now comes from Swift Package Manager, so its pod was removed from the Podfile " +
                "this Append build kept. Update the Pods integration before building in Xcode:\n\n" +
                $"    cd \"{buildPath}\" && pod install\n\n" +
                "If no other pods are left, run 'pod deintegrate' instead and open Unity-iPhone.xcodeproj, " +
                "or make a Replace build.");
        }
#endif
    }
}
