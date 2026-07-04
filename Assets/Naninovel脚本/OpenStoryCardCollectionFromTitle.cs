using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class OpenStoryCardCollectionFromTitle : MonoBehaviour
{
    public string collectionSceneName = "StoryCardCollection";
    public string titleSceneName = "Title";

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
