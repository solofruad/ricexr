using System;
using System.Collections.Generic;

namespace RiceXR.Core
{
    /// <summary>
    /// Forma del terreno flotante que se despliega bajo las hojas. Todas las distancias van en metros
    /// y en el espacio local del plano: X = ancho, Z = fondo, Y = normal de la superficie.
    /// </summary>
    public struct GroundShape
    {
        public float Width;
        public float Depth;
        /// <summary>Exponente de la superelipse del contorno: 2 = elipse; más alto = rectángulo más marcado.</summary>
        public float Sharpness;
        /// <summary>Hundimiento máximo del borde hacia dentro, modulado por ruido. Nunca se sale del plano.</summary>
        public float RimInset;
        /// <summary>Altura de la tapa sobre el origen del nivel (donde nacen las hojas).</summary>
        public float TopLift;
        /// <summary>Cuánto cae el labio del borde respecto a la tapa.</summary>
        public float LipDrop;
        /// <summary>Profundidad de la raíz de roca bajo el borde.</summary>
        public float RootDepth;
        public int Segments;
        public float SurfaceJitter;
        public float RootJitter;
        public int Seed;
    }

    /// <summary>Malla en arrays planos; el MonoBehaviour la vuelca a un Mesh de Unity.</summary>
    public sealed class GroundMeshData
    {
        /// <summary>xyz por vértice.</summary>
        public float[] Positions;
        /// <summary>uv por vértice, en metros (el tiling lo pone el material).</summary>
        public float[] Uvs;
        /// <summary>Submesh 0: tapa de pasto.</summary>
        public int[] TopTriangles;
        /// <summary>Submesh 1: raíz de roca.</summary>
        public int[] RootTriangles;
        /// <summary>Pares (a, b) de vértices duplicados en la costura de la raíz: deben compartir normal.</summary>
        public int[] SeamPairs;

        public int VertexCount => Positions.Length / 3;
    }

    /// <summary>
    /// Genera la "isla" low-poly: tapa plana con labio redondeado y raíz que se afila hacia abajo.
    /// Triángulos en el winding horario de Unity (cara frontal = normal de cross(b - a, c - a)).
    /// </summary>
    public static class GroundMeshBuilder
    {
        private const float TwoPi = MathF.PI * 2f;
        private const float LipStart = 0.82f;

        // Fracción del contorno de cada anillo de la tapa; el centro es un vértice aparte.
        private static readonly float[] TopRings = { 0.3f, 0.6f, LipStart, 0.93f, 1f };

        // Perfil de la raíz: escala del contorno y fracción de RootDepth por anillo. La punta cierra en 1.
        private static readonly float[] RootScale = { 1f, 0.97f, 0.8f, 0.52f, 0.24f };
        private static readonly float[] RootDepthFraction = { 0f, 0.18f, 0.45f, 0.74f, 0.92f };

        /// <summary>Punto del contorno para un ángulo (radianes, 0 = +X, crece hacia +Z).</summary>
        public static void OutlinePoint(in GroundShape shape, float angle, out float x, out float z)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            float exponent = 2f / MathF.Max(shape.Sharpness, 2f);

            x = shape.Width * 0.5f * MathF.Sign(cos) * MathF.Pow(MathF.Abs(cos), exponent);
            z = shape.Depth * 0.5f * MathF.Sign(sin) * MathF.Pow(MathF.Abs(sin), exponent);

            float length = MathF.Sqrt(x * x + z * z);
            if (length < 1e-5f) return;

            float inset = MathF.Min(shape.RimInset * RimNoise(shape.Seed, angle), length * 0.5f);
            float factor = (length - inset) / length;
            x *= factor;
            z *= factor;
        }

        /// <summary>
        /// Punto uniforme dentro del contorno. u y v en [0, 1]; maxT limita la distancia al centro
        /// como fracción del contorno (para no poner decoración sobre el labio).
        /// </summary>
        public static void SamplePoint(in GroundShape shape, float u, float v, float maxT, out float x, out float z)
        {
            OutlinePoint(shape, Clamp01(u) * TwoPi, out float rimX, out float rimZ);
            float t = MathF.Sqrt(Clamp01(v)) * Clamp01(maxT);
            x = rimX * t;
            z = rimZ * t;
        }

        public static GroundMeshData Build(in GroundShape shape)
        {
            if (shape.Width <= 0f || shape.Depth <= 0f)
                throw new ArgumentException("El terreno necesita ancho y fondo positivos.");

            int n = Math.Max(8, shape.Segments);
            var random = new Random(shape.Seed);
            var positions = new List<float>();
            var uvs = new List<float>();
            var top = new List<int>();
            var root = new List<int>();
            var seams = new List<int>();

            var rimX = new float[n];
            var rimZ = new float[n];
            var rimY = new float[n];
            for (int i = 0; i < n; i++)
                OutlinePoint(shape, i * TwoPi / n, out rimX[i], out rimZ[i]);

            // ── Tapa ────────────────────────────────────────────────────────────
            // El centro no lleva jitter: ahí nacen las hojas.
            AddVertex(positions, uvs, 0f, shape.TopLift, 0f, 0f, 0f);
            for (int r = 0; r < TopRings.Length; r++)
            {
                float t = TopRings[r];
                float lip = t <= LipStart ? 0f : (t - LipStart) / (1f - LipStart);
                float ringY = shape.TopLift - shape.LipDrop * lip * lip;
                bool isRim = r == TopRings.Length - 1;

                for (int i = 0; i < n; i++)
                {
                    float x = rimX[i] * t;
                    float z = rimZ[i] * t;
                    // El borde no lleva jitter propio: la raíz arranca exactamente en él.
                    float y = isRim ? ringY : ringY + Jitter(random, shape.SurfaceJitter);
                    if (isRim) rimY[i] = y;
                    AddVertex(positions, uvs, x, y, z, x, z);
                }
            }

            for (int i = 0; i < n; i++)
                AddTriangle(top, 0, TopIndex(n, 0, i + 1), TopIndex(n, 0, i));

            for (int r = 0; r < TopRings.Length - 1; r++)
            {
                for (int i = 0; i < n; i++)
                {
                    int inner = TopIndex(n, r, i);
                    int innerNext = TopIndex(n, r, i + 1);
                    int outer = TopIndex(n, r + 1, i);
                    int outerNext = TopIndex(n, r + 1, i + 1);
                    AddTriangle(top, inner, outerNext, outer);
                    AddTriangle(top, inner, innerNext, outerNext);
                }
            }

            // ── Raíz ────────────────────────────────────────────────────────────
            int rootStart = positions.Count / 3;
            int columns = n + 1;
            var perimeter = new float[columns];
            for (int i = 1; i < columns; i++)
            {
                int a = i - 1;
                int b = i % n;
                perimeter[i] = perimeter[a] + Distance(rimX[a], rimZ[a], rimX[b], rimZ[b]);
            }

            for (int j = 0; j < RootScale.Length; j++)
            {
                float firstX = 0f, firstY = 0f, firstZ = 0f;
                for (int i = 0; i < n; i++)
                {
                    float x = rimX[i];
                    float y = rimY[i];
                    float z = rimZ[i];

                    if (j > 0)
                    {
                        float radius = MathF.Max(MathF.Sqrt(x * x + z * z), 0.01f);
                        float scale = RootScale[j] + Jitter(random, shape.RootJitter) / radius;
                        scale = MathF.Min(MathF.Max(scale, 0.05f), 1f);
                        x *= scale;
                        z *= scale;

                        float depth = RootDepthFraction[j] * shape.RootDepth;
                        y = MathF.Min(rimY[i] - depth + Jitter(random, shape.RootJitter * 0.5f), rimY[i] - 0.005f);
                    }

                    if (i == 0) { firstX = x; firstY = y; firstZ = z; }
                    AddVertex(positions, uvs, x, y, z, perimeter[i], y);
                }

                // Costura: repite la primera columna con u = perímetro total para que la textura no se estire.
                AddVertex(positions, uvs, firstX, firstY, firstZ, perimeter[n], firstY);
                seams.Add(rootStart + j * columns);
                seams.Add(rootStart + j * columns + n);
            }

            int tip = positions.Count / 3;
            float tipY = shape.TopLift - shape.LipDrop - shape.RootDepth;
            AddVertex(positions, uvs,
                Jitter(random, shape.RootJitter), tipY, Jitter(random, shape.RootJitter),
                perimeter[n] * 0.5f, tipY);

            int last = RootScale.Length - 1;
            for (int j = 0; j < last; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    int upper = rootStart + j * columns + i;
                    int lower = upper + columns;
                    AddTriangle(root, upper, upper + 1, lower);
                    AddTriangle(root, upper + 1, lower + 1, lower);
                }
            }

            for (int i = 0; i < n; i++)
            {
                int upper = rootStart + last * columns + i;
                AddTriangle(root, upper, upper + 1, tip);
            }

            return new GroundMeshData
            {
                Positions = positions.ToArray(),
                Uvs = uvs.ToArray(),
                TopTriangles = top.ToArray(),
                RootTriangles = root.ToArray(),
                SeamPairs = seams.ToArray()
            };
        }

        // Ruido del borde en [0, 1]. Frecuencias enteras: el contorno cierra sin salto en 2π.
        private static float RimNoise(int seed, float angle)
        {
            float wave = MathF.Sin(3f * angle + Phase(seed, 1)) * 0.5f
                       + MathF.Sin(5f * angle + Phase(seed, 2)) * 0.3f
                       + MathF.Sin(11f * angle + Phase(seed, 3)) * 0.2f;
            return 0.5f + 0.5f * wave;
        }

        private static float Phase(int seed, int k)
        {
            double hash = Math.Sin(seed * 12.9898 + k * 78.233) * 43758.5453;
            return (float)((hash - Math.Floor(hash)) * Math.PI * 2.0);
        }

        private static float Jitter(Random random, float amplitude)
            => amplitude <= 0f ? 0f : (float)(random.NextDouble() * 2.0 - 1.0) * amplitude;

        private static int TopIndex(int n, int ring, int i) => 1 + ring * n + ((i % n) + n) % n;

        private static float Distance(float ax, float az, float bx, float bz)
            => MathF.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static void AddVertex(List<float> positions, List<float> uvs, float x, float y, float z, float u, float v)
        {
            positions.Add(x);
            positions.Add(y);
            positions.Add(z);
            uvs.Add(u);
            uvs.Add(v);
        }

        private static void AddTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}
