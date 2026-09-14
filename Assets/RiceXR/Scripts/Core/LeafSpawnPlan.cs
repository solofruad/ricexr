using System;
using System.Collections.Generic;

namespace RiceXR.Core
{
    public static class LeafSpawnPlan
    {
        public static int[] Create(IReadOnlyList<bool> diagnosable, int count, int required, Random random)
        {
            if (diagnosable == null || random == null || required < 1 || count < required)
                throw new ArgumentException("Configuración de generación inválida.");
            var candidates = new List<int>();
            for (int i = 0; i < diagnosable.Count; i++) if (diagnosable[i]) candidates.Add(i);
            if (candidates.Count == 0) throw new ArgumentException("No hay hojas diagnosticables.");
            var result = new int[count];
            for (int i = 0; i < count; i++) result[i] = i < required
                ? candidates[random.Next(candidates.Count)] : random.Next(diagnosable.Count);
            return result;
        }
    }
}
