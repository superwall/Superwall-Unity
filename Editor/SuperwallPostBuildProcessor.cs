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

        // The resource bundle SPM builds for SuperwallKit's SuperwallKit target.
        public const string SuperwallKitResourceBundle = "SuperwallKit_SuperwallKit.bundle";

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

            // Xcode builds SuperwallKit's resource bundle (Core Data model, receipt certificates,
            // images, localizations, privacy manifest) but doesn't copy it into UnityFramework, and
            // SuperwallKit stops with "unable to find bundle named SuperwallKit_SuperwallKit" when
            // it configures. Copy it in, where Bundle.module looks for a package linked into a
            // framework. Not in the app target: Unity as a Library apps don't have it.
            if (!project.ContainsFileByProjectPath(SuperwallKitResourceBundle))
            {
                string bundleGuid = project.AddFile(SuperwallKitResourceBundle, SuperwallKitResourceBundle, PBXSourceTree.Build);
                project.AddFileToBuild(unityFrameworkGuid, bundleGuid);
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
        // Pods integration. Drop SuperwallKit from it and reinstall the pods, or the workspace still
        // builds the old SuperwallKit pod and links it next to the Swift package: Xcode reports no
        // error, and the app gets two copies of every SuperwallKit class.
        // Runs before the processor opens project.pbxproj, because pod install rewrites it.
        private static void RemovePodFromAppendedBuild(string buildPath)
        {
            string podfilePath = Path.Combine(buildPath, "Podfile");
            if (!File.Exists(podfilePath)) return;

            string podfile = File.ReadAllText(podfilePath);
            string updated = RemoveSuperwallKitPod(podfile);
            if (updated == podfile) return;

            File.WriteAllText(podfilePath, updated);
            Debug.Log("[Superwall] SuperwallKit now comes from Swift Package Manager, so its pod was removed from the Podfile this Append build kept. Running pod install to update the Pods integration.");

            string failure = RunPodInstall(buildPath);
            if (failure != null)
            {
                Debug.LogWarning(
                    $"[Superwall] {failure} Until the Pods integration is updated, Unity-iPhone.xcworkspace still links " +
                    "the old SuperwallKit pod next to the Swift package. Run:\n\n" +
                    $"    cd \"{buildPath}\" && pod install\n\n" +
                    "or make a Replace build.");
            }
        }

        // Returns null on success, or why pod install didn't run or failed.
        private static string RunPodInstall(string buildPath)
        {
            string podPath = LocatePodExecutable();
            if (podPath == null) return "Could not find the CocoaPods 'pod' executable.";

            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "/bin/bash";
            // Login shell so the user's PATH from ~/.zshrc / ~/.bash_profile is loaded (rbenv/asdf shims, Homebrew).
            process.StartInfo.Arguments = $"-l -c \"cd '{buildPath}' && '{podPath}' install\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            // Some Ruby installs (rbenv/asdf) misbehave when these env vars are inherited from Unity.
            process.StartInfo.EnvironmentVariables.Remove("MONO_PATH");
            process.StartInfo.EnvironmentVariables.Remove("MONO_CFG_DIR");
            process.StartInfo.EnvironmentVariables.Remove("DYLD_FALLBACK_LIBRARY_PATH");
            process.StartInfo.EnvironmentVariables.Remove("DYLD_LIBRARY_PATH");
            // CocoaPods crashes with Encoding::CompatibilityError under non-UTF-8 locales;
            // Unity launched from Finder inherits launchd's empty LANG.
            process.StartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            process.StartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            try
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    Debug.Log($"[Superwall] pod install completed.\n{output}");
                    return null;
                }
                return $"pod install failed (exit code {process.ExitCode}):\n{error}{output}\n";
            }
            catch (System.Exception e)
            {
                return $"Could not run pod install ({e.Message}).";
            }
        }

        private static string LocatePodExecutable()
        {
            string home = System.Environment.GetEnvironmentVariable("HOME") ?? "";
            string[] candidates =
            {
                "/usr/local/bin/pod",
                "/opt/homebrew/bin/pod",
                Path.Combine(home, ".rbenv/shims/pod"),
                Path.Combine(home, ".asdf/shims/pod"),
                "/usr/bin/pod",
            };
            foreach (var path in candidates)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
            }

            // Fall back to `command -v pod` under a login shell.
            try
            {
                var which = new System.Diagnostics.Process();
                which.StartInfo.FileName = "/bin/bash";
                which.StartInfo.Arguments = "-l -c \"command -v pod\"";
                which.StartInfo.UseShellExecute = false;
                which.StartInfo.RedirectStandardOutput = true;
                which.StartInfo.RedirectStandardError = true;
                which.Start();
                string result = which.StandardOutput.ReadToEnd().Trim();
                which.WaitForExit();
                if (which.ExitCode == 0 && !string.IsNullOrEmpty(result) && File.Exists(result))
                {
                    return result;
                }
            }
            catch { }
            return null;
        }
#endif
    }
}
