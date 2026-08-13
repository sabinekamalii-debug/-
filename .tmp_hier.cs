var sb = new System.Text.StringBuilder();
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
sb.AppendLine("Scene: " + scene.name + " (" + scene.path + ")");
System.Action<UnityEngine.GameObject,int> print = null;
print = (go, depth) => {
    var indent = new string(' ', depth * 2);
    var comps = string.Join(", ", go.GetComponents<UnityEngine.Component>().Select(c => c == null ? "missing" : c.GetType().Name));
    sb.AppendLine(indent + go.name + " [" + comps + "] active=" + go.activeSelf);
    for (int i = 0; i < go.transform.childCount; i++)
        print(go.transform.GetChild(i).gameObject, depth + 1);
};
foreach (var root in scene.GetRootGameObjects())
    print(root, 0);
System.IO.File.WriteAllText("D:/unity/mowang/.scene_hierarchy.txt", sb.ToString(), System.Text.Encoding.UTF8);
return "OK, wrote " + sb.Length + " chars";
