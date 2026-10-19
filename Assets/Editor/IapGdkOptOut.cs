#if UNITY_EDITOR
using UnityEditor;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Keeps com.unity.purchasing from asking to install the Microsoft GDK. On a Windows editor the package's
    /// importer (PurchasingImporter) shows a MODAL "Required Dependency Missing" dialog offering com.unity.microsoft.gdk
    /// and its tools, which exist only for the Xbox Store. We ship on Android, the GDK is optional ("if these packages
    /// are not installed, the In-App Purchasing package will use FakeStore"), and a modal dialog stalls an automated
    /// editor session while installing the packages costs about ten minutes of import and an Xbox dependency in the
    /// manifest. The importer asks once per machine and remembers the answer in the editor pref below; this sets that
    /// pref to "already decided, do not install" before the importer's asynchronous package list returns, so the
    /// dialog never opens. A decision a person already made on this machine is left alone.
    /// </summary>
    [InitializeOnLoad]
    internal static class IapGdkOptOut
    {
        private const string DecisionKey = "IAP_GDKPackageInstall";

        static IapGdkOptOut()
        {
            if (!EditorPrefs.HasKey(DecisionKey)) EditorPrefs.SetBool(DecisionKey, true);
        }
    }
}
#endif
