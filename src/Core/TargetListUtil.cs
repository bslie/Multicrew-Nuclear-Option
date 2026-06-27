using System;
using System.Collections.Generic;

namespace SimpleWSO.Core
{
    public static class TargetListUtil
    {
        public static List<Unit> ValidTargets(IList<Unit> targets)
        {
            var result = new List<Unit>();
            if (targets == null) return result;

            foreach (var target in targets)
            {
                if (target != null && !target.disabled && !result.Contains(target))
                    result.Add(target);
            }

            return result;
        }

        public static uint[] BuildNetIds(IList<Unit> targets, int maxCount)
        {
            var ids = new List<uint>();
            if (targets == null || maxCount <= 0) return ids.ToArray();

            foreach (var target in targets)
            {
                if (target == null || target.disabled || target.NetId == 0 || ids.Contains(target.NetId))
                    continue;
                ids.Add(target.NetId);
                if (ids.Count >= maxCount)
                    break;
            }

            return ids.ToArray();
        }

        public static uint[] BuildPersistentIds(IList<Unit> targets, int maxCount)
        {
            var ids = new List<uint>();
            if (targets == null || maxCount <= 0) return ids.ToArray();

            foreach (var target in targets)
            {
                if (target == null || target.disabled || target.persistentID.NotValid || ids.Contains(target.persistentID.Id))
                    continue;
                ids.Add(target.persistentID.Id);
                if (ids.Count >= maxCount)
                    break;
            }

            return ids.ToArray();
        }

        public static List<Unit> ResolveNetIds(IEnumerable<uint> netIds, Func<uint, Unit> resolve)
        {
            var result = new List<Unit>();
            if (netIds == null || resolve == null) return result;

            foreach (uint netId in netIds)
            {
                if (netId == 0) continue;
                Unit unit = resolve(netId);
                if (unit != null && !unit.disabled && !result.Contains(unit))
                    result.Add(unit);
            }

            return result;
        }

        public static List<Unit> ResolvePersistentIds(IEnumerable<uint> persistentIds)
        {
            var result = new List<Unit>();
            if (persistentIds == null) return result;

            foreach (uint id in persistentIds)
            {
                if (id == 0) continue;
                var persistentID = new PersistentID { Id = id };
                if (persistentID.TryGetUnit(out Unit unit) && unit != null && !unit.disabled && !result.Contains(unit))
                    result.Add(unit);
            }

            return result;
        }
    }
}
