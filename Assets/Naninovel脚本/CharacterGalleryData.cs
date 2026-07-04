using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterGalleryData", menuName = "Naninovel/Character Gallery Data")]
public class CharacterGalleryData : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string id;
        public string displayName;
        public Sprite portrait;
        [TextArea(2, 4)] public string description;
    }

    public List<Entry> entries = new List<Entry>();
}
