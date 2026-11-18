namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Bounds how many short-lived, not-yet-verified flows can sit in memory at once, without
    /// looking at who sent them (no IPs). Flows are grouped by a scope (e.g. one listing):
    /// each scope keeps at most <c>perScopeCap</c> pending flows, and a new one pushes out that
    /// scope's oldest. A global cap is the backstop; when it is reached (after dropping expired
    /// entries) <see cref="TryAdd"/> refuses, so the caller can show a "busy, try again" message.
    /// The caller owns the actual flow objects (e.g. in IMemoryCache) and removes the ids this
    /// reports as evicted.
    /// </summary>
    public sealed class PendingFlowLimiter
    {
        private readonly object sync = new ();
        private readonly Dictionary<int, LinkedList<(Guid FlowId, DateTime ExpiresUtc)>> byScope = new ();
        private readonly int perScopeCap;
        private readonly int globalCap;
        private int total;

        public PendingFlowLimiter(int perScopeCap, int globalCap)
        {
            this.perScopeCap = Math.Max(1, perScopeCap);
            this.globalCap = Math.Max(this.perScopeCap, globalCap);
        }

        public int Count
        {
            get
            {
                lock (this.sync)
                {
                    return this.total;
                }
            }
        }

        public bool TryAdd(int scope, Guid flowId, DateTime expiresUtc, DateTime nowUtc, out List<Guid> evicted)
        {
            evicted = new List<Guid>();

            lock (this.sync)
            {
                if (this.byScope.TryGetValue(scope, out var list))
                {
                    this.PruneExpired(scope, list, nowUtc);
                }

                if (this.total >= this.globalCap)
                {
                    this.PruneAllExpired(nowUtc);
                }

                this.byScope.TryGetValue(scope, out list);

                // Room inside this scope is made by dropping its oldest, so a flood aimed at one
                // listing can't grow past the per-scope cap.
                while (list != null && list.Count >= this.perScopeCap)
                {
                    evicted.Add(list.First!.Value.FlowId);
                    list.RemoveFirst();
                    this.total--;
                }

                if (this.total >= this.globalCap)
                {
                    if (list != null && list.Count == 0)
                    {
                        this.byScope.Remove(scope);
                    }

                    return false;
                }

                if (list == null)
                {
                    list = new LinkedList<(Guid, DateTime)>();
                    this.byScope[scope] = list;
                }

                list.AddLast((flowId, expiresUtc));
                this.total++;
                return true;
            }
        }

        public void Remove(int scope, Guid flowId)
        {
            lock (this.sync)
            {
                if (!this.byScope.TryGetValue(scope, out var list))
                {
                    return;
                }

                for (var node = list.First; node != null; node = node.Next)
                {
                    if (node.Value.FlowId == flowId)
                    {
                        list.Remove(node);
                        this.total--;
                        break;
                    }
                }

                if (list.Count == 0)
                {
                    this.byScope.Remove(scope);
                }
            }
        }

        private void PruneExpired(int scope, LinkedList<(Guid FlowId, DateTime ExpiresUtc)> list, DateTime nowUtc)
        {
            for (var node = list.First; node != null;)
            {
                var next = node.Next;
                if (node.Value.ExpiresUtc <= nowUtc)
                {
                    list.Remove(node);
                    this.total--;
                }

                node = next;
            }

            if (list.Count == 0)
            {
                this.byScope.Remove(scope);
            }
        }

        private void PruneAllExpired(DateTime nowUtc)
        {
            foreach (var scope in this.byScope.Keys.ToList())
            {
                this.PruneExpired(scope, this.byScope[scope], nowUtc);
            }
        }
    }
}
