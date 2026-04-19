#define OBJECT_POOL_DEBUG

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Aor.Core.Pool
{
    // 统一的池创建接口
    public static class Pool
    {
        // ========== 无约束版本 ==========

        public static IPool<T> CreateGeneric<T>(Func<T> createFn, Action<T> resetFn = null,
            Action<T> releaseFn = null, int prewarmCount = 0, int maxCapacity = 999)
        {
#if OBJECT_POOL_DEBUG
        var innerPool = new GenericObjectPool<T>(createFn, resetFn, releaseFn, prewarmCount, maxCapacity);
        return PoolDiagnostics.AttachDebugger(innerPool);
#else
            return new GenericObjectPool<T>(createFn, resetFn, releaseFn, prewarmCount, maxCapacity);
#endif
        }

        public static IPool<T> CreateGeneric<T>(int prewarmCount = 0, int maxCapacity = 999) where T : new()
        {
            return CreateGeneric<T>(() => new T(), null, null, prewarmCount, maxCapacity);
        }

        // ========== Lock 版本（线程安全） ==========

        public static IPool<T> CreateLock<T>(Func<T> createFn, Action<T> resetFn = null,
            Action<T> releaseFn = null, int prewarmCount = 0, int maxCapacity = 999)
        {
#if OBJECT_POOL_DEBUG
        var innerPool = new LockObjectPool<T>(createFn, resetFn, releaseFn, prewarmCount, maxCapacity);
        return PoolDiagnostics.AttachDebugger(innerPool);
#else
            return new LockObjectPool<T>(createFn, resetFn, releaseFn, prewarmCount, maxCapacity);
#endif
        }

        public static IPool<T> CreateLock<T>(int prewarmCount = 0, int maxCapacity = 999) where T : new()
        {
            return CreateLock<T>(() => new T(), null, null, prewarmCount, maxCapacity);
        }

        // ========== Concurrent 版本（线程安全） ==========

        public static IPool<T> CreateConcurrent<T>(Func<T> createFn, Action<T> resetFn = null,
            Action<T> releaseFn = null, int prewarmCount = 0, int maxCapacity = 999)
        {
#if OBJECT_POOL_DEBUG
        var innerPool = new ConcurrentObjectPool<T>(createFn, resetFn, releaseFn, prewarmCount, maxCapacity);
        return PoolDiagnostics.AttachDebugger(innerPool);
#else
            return new ConcurrentObjectPool<T>(createFn, resetFn, releaseFn, prewarmCount, maxCapacity);
#endif
        }

        public static IPool<T> CreateConcurrent<T>(int prewarmCount = 0, int maxCapacity = 999) where T : new()
        {
            return CreateConcurrent<T>(() => new T(), null, null, prewarmCount, maxCapacity);
        }

        // ========== 实现 IPoolInstance 接口的便捷版本（Reset逻辑属于对象本身） ==========

        // （实现接口）
        public static IPool<T> CreateInterfaced<T>(int prewarmCount = 0, int maxCapacity = 999)
            where T : IPoolInstance, new()
        {
            return CreateGeneric(() => new T(), null, null, prewarmCount, maxCapacity);
        }

        // Lock（实现接口 + 线程安全）
        public static IPool<T> CreateInterfacedLock<T>(int prewarmCount = 0, int maxCapacity = 999)
            where T : IPoolInstance, new()
        {
            return CreateLock(() => new T(), null, null, prewarmCount, maxCapacity);
        }

        // Concurrent（实现接口 + 线程安全）
        public static IPool<T> CreateInterfacedConcurrent<T>(int prewarmCount = 0, int maxCapacity = 999)
            where T : IPoolInstance, new()
        {
            return CreateConcurrent(() => new T(), null, null, prewarmCount, maxCapacity);
        }
    }

    public interface IPoolInstance
    {
        void Reset();
    }

    public interface IPool<T>
    {
        T Get();
        void Return(T obj);
        void Prewarm(int preCount);
        void ClearTo(int cachedCount);
        int GetCount { get; }
        int MaxCapacity { get; }
        int CachedCount { get; }
    }

    public abstract class BaseObjectPool<T> : IPool<T>
    {
        protected int getCount = 0;
        protected readonly int maxCapacity;
        protected readonly Func<T> createFn;
        protected readonly Action<T> resetFn;
        protected readonly Action<T> releaseFn;

        public int GetCount => this.getCount;
        public int MaxCapacity => this.maxCapacity;
        public abstract int CachedCount { get; }

        protected BaseObjectPool(Func<T> createFn, Action<T> resetFn = null, Action<T> releaseFn = null,
            int prewarmCount = 0, int maxCapacity = 999)
        {
            this.createFn = createFn;
            this.resetFn = resetFn;
            this.releaseFn = releaseFn;
            this.maxCapacity = maxCapacity;
            if (prewarmCount > 0) this.Prewarm(prewarmCount);
        }


        protected abstract bool OnGet(out T obj);
        protected abstract void OnReturn(T obj);
        protected abstract void OnGetCountIncrease();
        protected abstract void OnGetCountDecrease();


        public T Get()
        {
            this.OnGetCountIncrease();

            T obj = this.OnGet(out T cached) ? cached : this.createFn();

            if (this.resetFn != null)
                this.resetFn(obj);
            else if(obj is IPoolInstance)
                (obj as IPoolInstance).Reset();

            return obj;
        }

        public void Return(T obj)
        {
            if (obj == null)
            {
                Debug.LogError("Trying to return null object to pool.");
                return;
            }

            if (this.releaseFn != null && this.CachedCount >= this.maxCapacity)
                this.releaseFn(obj);
            else
                this.OnReturn(obj);

            this.OnGetCountDecrease();
        }

        public void Prewarm(int preCount)
        {
            int targetCount = Math.Min(preCount, this.maxCapacity);
            while (this.CachedCount < targetCount)
            {
                this.OnReturn(this.createFn());
            }
        }

        public void ClearTo(int cachedCount)
        {
            int target = Mathf.Clamp(cachedCount, 0, this.CachedCount);
            while (this.CachedCount > target && this.OnGet(out T obj))
            {
                this.releaseFn?.Invoke(obj);
                this.OnGetCountDecrease();
            }
        }
    }

    public sealed class GenericObjectPool<T> : BaseObjectPool<T>
    {
        private readonly Stack<T> poolStack = new Stack<T>();

        public override int CachedCount => this.poolStack.Count;

        public GenericObjectPool(Func<T> createFn, Action<T> resetFn = null, Action<T> releaseFn = null,
            int prewarmCount = 0, int maxCapacity = 999)
            : base(createFn, resetFn, releaseFn, prewarmCount, maxCapacity)
        {
        }

        protected override bool OnGet(out T obj)
        {
            if (this.poolStack.Count > 0)
            {
                obj = this.poolStack.Pop();
                return true;
            }
            obj = default;
            return false;
        }

        protected override void OnReturn(T obj) => this.poolStack.Push(obj);
            
        protected override void OnGetCountIncrease() => this.getCount++;

        protected override void OnGetCountDecrease()=> this.getCount--;
    }

    public sealed class LockObjectPool<T> : BaseObjectPool<T>
    {
        private readonly Stack<T> poolStack = new Stack<T>();
        private readonly object poolLock = new object();

        public override int CachedCount {
            get {
                lock (this.poolLock)
                {
                    return this.poolStack.Count;
                }
            }
        }

        public LockObjectPool(Func<T> createFn, Action<T> resetFn = null, Action<T> releaseFn = null,
            int prewarmCount = 0, int maxCapacity = 999)
            : base(createFn, resetFn, releaseFn, prewarmCount, maxCapacity)
        {
        }

        protected override bool OnGet(out T obj)
        {
            lock (this.poolLock)
            {
                if (this.poolStack.Count > 0)
                {
                    obj = this.poolStack.Pop();
                    return true;
                }
                obj = default;
                return false;
            }
        }

        protected override void OnReturn(T obj)
        {
            lock (this.poolLock)
            {
                this.poolStack.Push(obj);
            }
        }

        protected override void OnGetCountIncrease()
        {
            lock (this.poolLock)
            {
                this.getCount++;
            }
        }

        protected override void OnGetCountDecrease()
        {
            lock (this.poolLock)
            {
                this.getCount--;
            }
        }
    }

    public sealed class ConcurrentObjectPool<T> : BaseObjectPool<T>
    {
        private readonly System.Collections.Concurrent.ConcurrentStack<T> poolStack = new System.Collections.Concurrent.ConcurrentStack<T>();

        public override int CachedCount => this.poolStack.Count;

        public ConcurrentObjectPool(Func<T> createFn, Action<T> resetFn = null, Action<T> releaseFn = null,
            int prewarmCount = 0, int maxCapacity = 999)
            : base(createFn, resetFn, releaseFn, prewarmCount, maxCapacity)
        {
        }

        protected override bool OnGet(out T obj)=> this.poolStack.TryPop(out obj);

        protected override void OnReturn(T obj) => this.poolStack.Push(obj);

        protected override void OnGetCountIncrease() => Interlocked.Increment(ref this.getCount);

        protected override void OnGetCountDecrease() => Interlocked.Decrement(ref this.getCount);
    }

#if OBJECT_POOL_DEBUG
    // 调试相关代码
    public interface IPoolDebugger<T>
    {
        void OnGet(T obj, string stackTrace);
        void OnReturn(T obj);
        void OnClearTo(int cachedCount);
        void PrintTimeoutStatus(float timeoutDuration);
    }

    public class PoolDebugger<T> : IPoolDebugger<T>
    {
        private class DebugInfo
        {
            public T Object { get; set; }
            public string BorrowTrace { get; set; }
            public float BorrowTime { get; set; }
            public float LastErrorTime { get; set; }
        }

        private readonly Dictionary<T, DebugInfo> borrowedItems = new Dictionary<T, DebugInfo>();
        private readonly string poolName;

        public PoolDebugger(string poolName = null)
        {
            this.poolName = poolName ?? typeof(T).Name;
        }

        public void OnGet(T obj, string stackTrace)
        {
            this.borrowedItems[obj] = new DebugInfo
            {
                Object = obj,
                BorrowTrace = stackTrace,
                BorrowTime = Time.time,
                LastErrorTime = 0
            };
            Debug.Log($"[Pool:{this.poolName}] Get: {obj.GetHashCode()}, borrowed: {this.borrowedItems.Count}");
        }

        public void OnReturn(T obj)
        {
            if (!this.borrowedItems.Remove(obj))
            {
                Debug.LogWarning($"[Pool:{this.poolName}] Object not borrowed: {obj.GetHashCode()}");
            }
        }

        // 打印当前借出对象的状态，包含超时对象数量和最久未还对象的信息
        public void PrintTimeoutStatus(float timeoutDuration = 30)
        {
            if (this.borrowedItems.Count == 0)
            {
                Debug.Log($"[Pool:{this.poolName}] Status: all objects returned");
                return;
            }

            DebugInfo oldest = null;
            T oldestObj = default;
            int timeoutCount = 0;
            float oldestDuration = 0;

            foreach (var kvp in this.borrowedItems)
            {
                var info = kvp.Value;
                float borrowDuration = Time.time - info.BorrowTime;

                if (borrowDuration > timeoutDuration && Time.time - info.LastErrorTime > timeoutDuration)
                {
                    timeoutCount++;
                    info.LastErrorTime = Time.time;
                }

                if (oldest == null || borrowDuration > oldestDuration)
                {
                    oldest = info;
                    oldestObj = kvp.Key;
                    oldestDuration = borrowDuration;
                }
            }

            Debug.LogWarning($"[Pool:{this.poolName}] Status - Borrowed: {this.borrowedItems.Count}, Timeout: {timeoutCount}\n" +
                $"Oldest object: {oldestObj}, time: {oldestDuration:F2}s, trace:\n{oldest?.BorrowTrace}");
        }

        public void OnClearTo(int cachedCount)
        {
            Debug.Log($"[Pool:{this.poolName}] ClearTo: {cachedCount}");
        }
    }

    public class ObjectPoolDebugProxy<T> : IPool<T>
    {
        private readonly IPool<T> targetPool;
        private readonly IPoolDebugger<T> targetDebugger;

        public int GetCount => this.targetPool.GetCount;
        public int MaxCapacity => this.targetPool.MaxCapacity;
        public int CachedCount => this.targetPool.CachedCount;

        public ObjectPoolDebugProxy(IPool<T> target, IPoolDebugger<T> debugger)
        {
            this.targetPool = target;
            this.targetDebugger = debugger;
        }

        public T Get()
        {
            var obj = this.targetPool.Get();
            this.targetDebugger.OnGet(obj, StackTraceUtility.ExtractStackTrace());
            return obj;
        }

        public void Return(T obj)
        {
            this.targetDebugger.OnReturn(obj);
            this.targetPool.Return(obj);
        }

        public void Prewarm(int preCount) => this.targetPool.Prewarm(preCount);

        public void ClearTo(int cachedCount)
        {
            this.targetDebugger.OnClearTo(cachedCount);
            this.targetPool.ClearTo(cachedCount);
        }
    }

    public static class PoolDiagnostics
    {
        public static IPool<T> AttachDebugger<T>(IPool<T> pool, IPoolDebugger<T> debugger = null)
        {
            if (debugger == null)
                debugger = new PoolDebugger<T>();
            
            return new ObjectPoolDebugProxy<T>(pool, debugger);
        }
    }
#endif
}