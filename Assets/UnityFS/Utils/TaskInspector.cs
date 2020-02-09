
namespace UnityFS.Utils
{
    using UnityEngine;

    public class TaskInspector : MonoBehaviour
    {
        private void DrawText(float x, float y, float h, string text, Color color)
        {
            var oldColor = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(x - 1f, y - 1f, 1000f, h), text);
            GUI.Label(new Rect(x - 1f, y, 1000f, h), text);
            GUI.Label(new Rect(x - 1f, y + 0f, 1000f, h), text);

            GUI.Label(new Rect(x + 1f, y - 1f, 1000f, h), text);
            GUI.Label(new Rect(x + 1f, y, 1000f, h), text);
            GUI.Label(new Rect(x + 1f, y + 1f, 1000f, h), text);

            GUI.Label(new Rect(x, y - 1f, 1000f, h), text);
            GUI.Label(new Rect(x, y + 1f, 1000f, h), text);
            GUI.color = color;
            GUI.Label(new Rect(x, y, 1000f, h), text);
            GUI.color = oldColor;
        }

        void OnGUI()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            var scale = 2f;
            GUI.matrix = Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(scale, scale, scale)
            );
            var x = 10f;
            var y = 0f;
            var line = 20f;
            var assetProvider = UnityFS.ResourceManager.GetAssetProvider();

            GUILayout.BeginVertical();
            assetProvider.ForEachTask(task =>
            {
                if (task.isRunning)
                {
                    DrawText(x, y, line, string.Format("{0} {1} {2:00.00}%", task.name, task.size, task.progress * 100f), Color.green);

                }
                else
                {
                    DrawText(x, y, line, string.Format("{0} {1}", task.name, task.size), Color.white);
                }
                y += line;
            });
            GUILayout.EndVertical();
        }
    }
}