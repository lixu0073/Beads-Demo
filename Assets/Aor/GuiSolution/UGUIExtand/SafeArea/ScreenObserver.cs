using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;

namespace Aor.UI
{
    public static class ScreenObserver
    {
        public readonly struct ScreenSnapshot
        {
            public readonly Rect SafeArea;
            public readonly Vector2Int Resolution;
            public readonly ScreenOrientation Orientation;
            public readonly bool IsPortrait;

            public ScreenSnapshot(Rect sa, Vector2Int res, ScreenOrientation orientation)
            {
                SafeArea = sa;
                Resolution = res;
                Orientation = orientation;
                IsPortrait = res.y > res.x;
            }
        }

        // 观察分辨率
        public static IUniTaskAsyncEnumerable<ScreenSnapshot> ObserveScreenChanges(CancellationToken token)
        {
            return UniTaskAsyncEnumerable.EveryUpdate(PlayerLoopTiming.LastPostLateUpdate)
                .Select(_ => new ScreenSnapshot(Screen.safeArea, new Vector2Int(Screen.width, Screen.height), Screen.orientation))
                .DistinctUntilChanged();
        }

        // 观察焦点
        public static IUniTaskAsyncEnumerable<bool> ObserveAppFocus(CancellationToken token)
        {
            return UniTaskAsyncEnumerable.Create<bool>(async (writer, t) => {
                bool lastFocus = Application.isFocused;
                await writer.YieldAsync(lastFocus);

                while (!t.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, t);
                    bool currentFocus = Application.isFocused;
                    if (currentFocus != lastFocus)
                    {
                        lastFocus = currentFocus;
                        await writer.YieldAsync(currentFocus);
                    }
                }
            });
        }
    }
}