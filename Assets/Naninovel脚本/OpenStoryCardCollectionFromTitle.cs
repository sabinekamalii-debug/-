using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class OpenStoryCardCollectionFromTitle : MonoBehaviour
{
    public string collectionSceneName = SceneNames.StoryCardCollection;
    public string titleSceneName = SceneNames.Title;

    void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnOpenCollection);
    }

    public void OnOpenCollection()
    {
        RogueFlowRouter.SetReturnSceneBeforeOpeningCollection(titleSceneName);
        VideoSceneLoader.LoadScene(collectionSceneName);
    }
}
