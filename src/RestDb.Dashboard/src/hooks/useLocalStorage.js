import { useCallback, useState } from 'react';

export function useLocalStorage(key, initialValue) {
  const [value, setValue] = useState(() => {
    if (typeof window === 'undefined') {
      return initialValue;
    }

    const storedValue = window.localStorage.getItem(key);
    if (!storedValue) {
      return initialValue;
    }

    try {
      return JSON.parse(storedValue);
    } catch {
      return initialValue;
    }
  });

  const updateValue = useCallback(
    (nextValue) => {
      setValue(nextValue);
      if (typeof window !== 'undefined') {
        window.localStorage.setItem(key, JSON.stringify(nextValue));
      }
    },
    [key]
  );

  const removeValue = useCallback(() => {
    setValue(initialValue);
    if (typeof window !== 'undefined') {
      window.localStorage.removeItem(key);
    }
  }, [initialValue, key]);

  return [value, updateValue, removeValue];
}
