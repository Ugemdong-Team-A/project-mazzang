using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component가 붙은 GameObject의 생성, 대여, 반환 수명주기를 관리합니다.
/// </summary>
public sealed class GameObjectPool<T>
    where T : Component
{
    private readonly Queue<T> _available =
        new();

    private readonly HashSet<T> _availableLookup =
        new();

    private Func<T> _createFunc;
    private Action<T> _onGet;
    private Action<T> _onRelease;
    private Action<T> _onDestroy;

    public int Count =>
        _available.Count;


    public GameObjectPool()
    {
    }


    public GameObjectPool(
        int initialCount,
        Func<T> createFunc,
        Action<T> onGet = null,
        Action<T> onRelease = null,
        Action<T> onDestroy = null)
    {
        CreatePool(
            initialCount,
            createFunc,
            onGet,
            onRelease,
            onDestroy);
    }


    public void CreatePool(
        int initialCount,
        Func<T> createFunc,
        Action<T> onGet = null,
        Action<T> onRelease = null,
        Action<T> onDestroy = null)
    {
        if (createFunc == null)
        {
            throw new ArgumentNullException(
                nameof(createFunc));
        }

        Clear();

        _createFunc =
            createFunc;

        _onGet =
            onGet;

        _onRelease =
            onRelease;

        _onDestroy =
            onDestroy;

        int count =
            Mathf.Max(
                0,
                initialCount);

        for (int i = 0;
             i < count;
             i++)
        {
            T item =
                CreateItem();

            StoreAvailableItem(
                item);
        }
    }


    public T Get()
    {
        while (_available.Count > 0)
        {
            T item =
                _available.Dequeue();

            _availableLookup.Remove(
                item);

            if (item == null)
                continue;

            _onGet?.Invoke(
                item);

            return item;
        }

        T created =
            CreateItem();

        _onGet?.Invoke(
            created);

        return created;
    }


    public bool Release(
        T item)
    {
        if (item == null ||
            _availableLookup.Contains(
                item))
        {
            return false;
        }

        StoreAvailableItem(
            item);

        return true;
    }


    public void Clear()
    {
        while (_available.Count > 0)
        {
            T item =
                _available.Dequeue();

            if (item == null)
                continue;

            if (_onDestroy != null)
            {
                _onDestroy(
                    item);
            }
            else
            {
                UnityEngine.Object.Destroy(
                    item.gameObject);
            }
        }

        _availableLookup.Clear();
    }


    private T CreateItem()
    {
        if (_createFunc == null)
        {
            throw new InvalidOperationException(
                "Pool 생성 함수가 설정되지 않았습니다.");
        }

        T item =
            _createFunc();

        if (item == null)
        {
            throw new InvalidOperationException(
                "Pool 생성 함수가 null을 반환했습니다.");
        }

        return item;
    }


    private void StoreAvailableItem(
        T item)
    {
        _onRelease?.Invoke(
            item);

        if (item == null ||
            !_availableLookup.Add(
                item))
        {
            return;
        }

        _available.Enqueue(
            item);
    }
}
