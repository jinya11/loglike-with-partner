using System.Collections.Generic;
using UnityEngine;

public interface IPool { void Release(Poolable p); }

/// <summary>풀에서 꺼내 쓰는 오브젝트. Despawn으로 반환.</summary>
public abstract class Poolable : MonoBehaviour
{
    internal IPool owner;
    public void Despawn()
    {
        if (owner != null) owner.Release(this);
        else Destroy(gameObject);
    }
}

/// <summary>매 프레임 StageDirector가 일괄 Tick하는 풀 오브젝트. Update 콜백 수를 줄이기 위함.</summary>
public abstract class Ticked<T> : Poolable where T : Ticked<T>
{
    public static readonly List<T> Active = new List<T>(256);
    int index = -1;

    protected virtual void OnEnable()
    {
        index = Active.Count;
        Active.Add((T)this);
    }

    protected virtual void OnDisable()
    {
        if (index < 0) return;
        int last = Active.Count - 1;
        T moved = Active[last];
        Active[index] = moved;
        moved.index = index;
        Active.RemoveAt(last);
        index = -1;
    }

    public abstract void Tick(float dt);

    /// <summary>역순 순회: 현재 원소가 Despawn되어도 안전.</summary>
    public static void TickAll(float dt)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
            if (i < Active.Count) Active[i].Tick(dt);
    }
}

public sealed class Pool<T> : IPool where T : Poolable
{
    readonly T prefab;
    readonly Transform root;
    readonly Stack<T> free;

    public Pool(T prefab, Transform root, int prewarm)
    {
        this.prefab = prefab;
        this.root = root;
        free = new Stack<T>(prewarm);
        for (int i = 0; i < prewarm; i++)
        {
            T t = Create();
            t.gameObject.SetActive(false);
            free.Push(t);
        }
    }

    T Create()
    {
        T t = Object.Instantiate(prefab, root);
        t.owner = this;
        return t;
    }

    public T Get(Vector3 pos, Quaternion rot)
    {
        T t = free.Count > 0 ? free.Pop() : Create();
        t.transform.SetPositionAndRotation(pos, rot);
        t.gameObject.SetActive(true);
        return t;
    }

    public void Release(Poolable p)
    {
        if (!p.gameObject.activeSelf) return;
        p.gameObject.SetActive(false);
        free.Push((T)p);
    }
}
