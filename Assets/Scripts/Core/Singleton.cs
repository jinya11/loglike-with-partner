using UnityEngine;

/// <summary>씬 단위(기본) 또는 영구(Persistent) 싱글톤.</summary>
public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    public static T I { get; private set; }
    protected virtual bool Persistent => false;

    protected virtual void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = (T)this;
        if (Persistent) DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (I == this) I = null;
    }
}
