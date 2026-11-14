using UnityEditor;
using UnityEngine;

namespace AlchemyTower.Editor
{
    public static class SaveResetMenu
    {
        private const string MenuPath = "Tools/Alchemy Tower/Reset Local Save";

        [MenuItem(MenuPath)]
        private static void ResetLocalSave()
        {
            string playModeNote = EditorApplication.isPlaying
                ? " Play Mode will stop first so the running game cannot recreate the old save."
                : string.Empty;
            if (!EditorUtility.DisplayDialog(
                    "Reset Alchemy Tower Save",
                    "Delete only the local Alchemy Tower save?" + playModeNote
                    + " Start Play Mode again to begin a fresh session.",
                    "Delete Save",
                    "Cancel"))
            {
                return;
            }

            if (EditorApplication.isPlaying)
            {
                TowerController controller = Object.FindFirstObjectByType<TowerController>();
                controller?.DisableSavingForSession();
                DeleteSave();
                EditorApplication.isPlaying = false;
                return;
            }

            DeleteSave();
        }

        private static void DeleteSave()
        {
            new PlayerPrefsSaveStorage().Delete();
            Debug.Log("Alchemy Tower local save deleted. Start Play Mode again for a fresh session.");
        }
    }
}
