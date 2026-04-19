using UnityEngine;
using Aor.Core.Pool;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class ObjectPoolTest : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private int maxPoolSize = 10;
    [SerializeField] private int prewarmCount = 5;
    [SerializeField] private PoolType poolType = PoolType.Concurrent;

    [Header("ClearTo Settings")]
    [SerializeField] private int clearToCount = 3;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button spawnButton;
    [SerializeField] private Button returnButton;
    [SerializeField] private Button returnAllButton;
    [SerializeField] private Button clearToButton;
    [SerializeField] private Button clearAllButton;
    [SerializeField] private Button printStatusButton;
    [SerializeField] private TMP_Dropdown poolTypeDropdown;

    private IPool<GameObject> _pool;
    private Stack<GameObject> _activeObjects = new Stack<GameObject>();

    private int _totalSpawned;
    private int _totalReturned;

    public enum PoolType
    {
        Generic,
        Lock,
        Concurrent
    }

    void Awake()
    {
        CreatePool();
        SetupUI();
        UpdateUI();
    }

    void SetupUI()
    {
        if (spawnButton != null)
            spawnButton.onClick.AddListener(SpawnObject);

        if (returnButton != null)
            returnButton.onClick.AddListener(ReturnLastObject);

        if (returnAllButton != null)
            returnAllButton.onClick.AddListener(ReturnAllObjects);

        if (clearToButton != null)
            clearToButton.onClick.AddListener(ClearTo);

        if (clearAllButton != null)
            clearAllButton.onClick.AddListener(ClearAllObjects);

        if (printStatusButton != null)
            printStatusButton.onClick.AddListener(PrintPoolStatus);

        if (poolTypeDropdown != null)
        {
            poolTypeDropdown.ClearOptions();
            poolTypeDropdown.AddOptions(new List<string> { "Generic", "Lock", "Concurrent" });
            poolTypeDropdown.value = (int)poolType;
            poolTypeDropdown.onValueChanged.AddListener(OnPoolTypeChanged);
        }
    }

    void OnPoolTypeChanged(int index)
    {
        poolType = (PoolType)index;
        ReturnAllObjects();
        CreatePool();
        Debug.Log($"切换到 {poolType} 池");
        UpdateUI();
    }

    void CreatePool()
    {
        switch (poolType)
        {
            case PoolType.Generic:
                _pool = Pool.CreateGeneric<GameObject>(
                    createFn: CreateInstance,
                    resetFn: ResetInstance,
                    releaseFn: ReleaseInstance,
                    prewarmCount: prewarmCount,
                    maxCapacity: maxPoolSize
                );
                Debug.Log("创建 GenericObjectPool");
                break;

            case PoolType.Lock:
                _pool = Pool.CreateLock<GameObject>(
                    createFn: CreateInstance,
                    resetFn: ResetInstance,
                    releaseFn: ReleaseInstance,
                    prewarmCount: prewarmCount,
                    maxCapacity: maxPoolSize
                );
                Debug.Log("创建 LockObjectPool");
                break;

            case PoolType.Concurrent:
                _pool = Pool.CreateConcurrent<GameObject>(
                    createFn: CreateInstance,
                    resetFn: ResetInstance,
                    releaseFn: ReleaseInstance,
                    prewarmCount: prewarmCount,
                    maxCapacity: maxPoolSize
                );
                Debug.Log("创建 ConcurrentObjectPool");
                break;
        }
    }

    GameObject CreateInstance()
    {
        GameObject go = Instantiate(prefab);
        go.SetActive(false);
        Debug.Log($"[Create] 创建新对象: {go.GetHashCode()}");
        return go;
    }

    void ResetInstance(GameObject go)
    {
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.SetActive(false);
    }

    void ReleaseInstance(GameObject go)
    {
        Debug.Log($"[Release] 销毁对象: {go.GetHashCode()}");
        Destroy(go);
    }

    void SpawnObject()
    {
        if (prefab == null)
        {
            Debug.LogError("Prefab 未赋值！");
            return;
        }

        GameObject go = _pool.Get();

        Vector3 randomPos = new Vector3(
            Random.Range(-8f, 8f),
            Random.Range(-4f, 4f),
            0
        );
        go.transform.position = randomPos;

        Image img = go.GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(Random.value, Random.value, Random.value);
        }

        float scale = Random.Range(0.5f, 1.5f);
        go.transform.localScale = new Vector3(scale, scale, 1);

        go.SetActive(true);

        _activeObjects.Push(go);
        _totalSpawned++;

        Debug.Log($"[Spawn] 生成对象: {go.GetHashCode()}, 当前活跃: {_activeObjects.Count}, 池缓存: {_pool.CachedCount}");
        UpdateUI();
    }

    void ReturnLastObject()
    {
        if (_activeObjects.Count > 0)
        {
            GameObject go = _activeObjects.Pop();
            ReturnToPool(go);
        } else
        {
            Debug.LogWarning("没有可归还的对象，请先生成。");
        }
    }

    void ReturnAllObjects()
    {
        int count = _activeObjects.Count;
        while (_activeObjects.Count > 0)
        {
            GameObject go = _activeObjects.Pop();
            ReturnToPool(go);
        }
        Debug.Log($"一次性归还 {count} 个对象到池子");
        UpdateUI();
    }

    void ReturnToPool(GameObject go)
    {
        if (go == null) return;

        go.SetActive(false);
        go.transform.position = Vector3.zero;

        _pool.Return(go);
        _totalReturned++;

        Debug.Log($"[Return] 归还对象: {go.GetHashCode()}, 池缓存: {_pool.CachedCount}");
    }

    void ClearTo()
    {
        int beforeCount = _pool.CachedCount;
        _pool.ClearTo(clearToCount);
        int afterCount = _pool.CachedCount;
        int releasedCount = beforeCount - afterCount;

        Debug.Log($"[ClearTo] 目标缓存: {clearToCount}, 之前: {beforeCount}, 之后: {afterCount}, 释放了: {releasedCount} 个对象");
        UpdateUI();
    }

    void ClearAllObjects()
    {
        ReturnAllObjects();
        _pool.ClearTo(0);
        Debug.Log($"清空池子，当前缓存: {_pool.CachedCount}");
        UpdateUI();
    }

    void PrintPoolStatus()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("========== 对象池状态 ==========");
        sb.AppendLine($"池类型: {poolType}");
        sb.AppendLine($"当前借出: {_pool.GetCount}");
        sb.AppendLine($"当前缓存: {_pool.CachedCount}");
        sb.AppendLine($"最大容量: {_pool.MaxCapacity}");
        sb.AppendLine($"活跃对象: {_activeObjects.Count}");
        sb.AppendLine($"总生成数: {_totalSpawned}");
        sb.AppendLine($"总归还数: {_totalReturned}");
        sb.AppendLine($"未归还: {_totalSpawned - _totalReturned}");
        sb.AppendLine($"使用率: {(_totalSpawned > 0 ? (float)(_totalSpawned - _totalReturned) / _totalSpawned * 100 : 0):F1}%");

        Debug.Log(sb.ToString());
    }

    void UpdateUI()
    {
        if (statusText == null) return;

        var sb = new System.Text.StringBuilder();

        sb.Append("=== 对象池测试 ===\n");
        sb.Append($"池类型: {poolType}\n");
        sb.Append($"借出: {_pool.GetCount} | 缓存: {_pool.CachedCount}\n");
        sb.Append($"最大: {_pool.MaxCapacity} | 活跃: {_activeObjects.Count}\n");
        sb.Append($"生成: {_totalSpawned} | 归还: {_totalReturned}\n");
        sb.Append($"未归还: {_totalSpawned - _totalReturned}\n");
        sb.Append($"使用率: {(_totalSpawned > 0 ? (float)(_totalSpawned - _totalReturned) / _totalSpawned * 100 : 0):F0}%\n\n");

        sb.Append("=== ClearTo 设置 ===\n");
        sb.Append($"目标缓存数: {clearToCount}\n\n");

        sb.Append("操作说明:\n");
        sb.Append("生成: 从池中获取对象\n");
        sb.Append("归还: 归还最新对象\n");
        sb.Append("全部归还: 归还所有对象\n");
        sb.Append($"ClearTo({clearToCount}): 清理缓存到指定数量\n");
        sb.Append("清空: 清空所有对象\n");
        sb.Append("打印状态: 输出详细日志");

        statusText.text = sb.ToString();
    }

    void OnDestroy()
    {
        ReturnAllObjects();
        PrintPoolStatus();
    }
}