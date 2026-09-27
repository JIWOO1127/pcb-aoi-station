import { useEffect, useState } from 'react';

export function useApi(loader, initialValue = []) {
  const [data, setData] = useState(initialValue);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    let active = true;
    setLoading(true);
    loader().then((result) => { if (active) { setData(result); setLoading(false); } });
    return () => { active = false; };
  }, [loader]);
  return { data, setData, loading };
}
