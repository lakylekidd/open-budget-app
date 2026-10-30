// Shares the Long-Term Planning year between useAppRouter (/{budgetKey}/long-term/{year}) and LongTermPlanning
// without threading props through App.tsx. `null` means no router is mounted (e.g. LongTermPlanning rendered alone),
// in which case LongTermPlanning keeps its own year exactly as before.
import { useSyncExternalStore } from 'react';

let routedYear: number | null = null;
const listeners = new Set<() => void>();

export const longTermYearStore = {
  get: () => routedYear,
  set(year: number | null) {
    if (routedYear === year) return;
    routedYear = year;
    listeners.forEach(listener => listener());
  },
  subscribe(listener: () => void) {
    listeners.add(listener);
    return () => { listeners.delete(listener); };
  },
};

export function useRoutedLongTermYear(): number | null {
  return useSyncExternalStore(longTermYearStore.subscribe, longTermYearStore.get, longTermYearStore.get);
}
