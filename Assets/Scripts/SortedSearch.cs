using System;
using System.Collections.Generic;

// 오름차순 정렬된 IList에서 이진 탐색 기반 조회를 제공하는 유틸리티다.
// 틱 루프 외부의 희소 조회에 사용하며, keySelector 호출 비용이 낮은 경우를 전제한다.
public static class SortedSearch
{
    // 오름차순 정렬된 list에서 keySelector(element)가 target에 가장 가까운 요소의 인덱스를 반환한다.
    // 동률이면 더 작은 인덱스(앞쪽 요소)를 반환한다. list가 비어 있으면 -1을 반환한다.
    public static int NearestIndex<T>(IList<T> list, float target, Func<T, float> keySelector)
    {
        if (list == null || list.Count == 0)
            return -1;

        int lo = 0,
            hi = list.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (keySelector(list[mid]) < target)
                lo = mid + 1;
            else
                hi = mid;
        }

        if (lo > 0)
        {
            float distCurrent = Math.Abs(keySelector(list[lo]) - target);
            float distPrev = Math.Abs(keySelector(list[lo - 1]) - target);
            if (distPrev <= distCurrent)
                lo--;
        }

        return lo;
    }
}
