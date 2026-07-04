using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int poolSize = 5;
    private List<GameObject> _pool;
    void Start()
    {
        _pool = new List<GameObject>();
        for (int i = 0; i < poolSize; i++)
        {
            CreateNewObject();
        }
    }
    private GameObject CreateNewObject()
    {
        GameObject obj = Instantiate(prefab,transform);
        obj.SetActive(false);
        _pool.Add(obj);
        return obj;
    }
    public GameObject GetPooledObject()
    {
        if (_pool == null) return null;
        if (prefab == null) return null;
        foreach (GameObject obj in _pool)
        {
            if (obj != null && !obj.activeSelf)
                return obj;
        }
        return CreateNewObject();
    }
}
