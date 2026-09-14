using System;
using NUnit.Framework;
using RiceXR.Core;

namespace RiceXR.Tests.EditMode
{
    /// <summary>
    /// Cubre la geometría del terreno flotante que usa SurfaceGroundController: que no se salga del
    /// plano seleccionado, que la malla sea válida y que las caras miren hacia fuera.
    /// </summary>
    public class GroundMeshBuilderTests
    {
        private static GroundShape Shape(float width = 1.2f, float depth = 0.7f, int seed = 7, float jitter = 0f)
        {
            return new GroundShape
            {
                Width = width,
                Depth = depth,
                Sharpness = 5f,
                RimInset = 0.03f,
                TopLift = 0.008f,
                LipDrop = 0.015f,
                RootDepth = 0.2f,
                Segments = 48,
                SurfaceJitter = jitter * 0.15f,
                RootJitter = jitter,
                Seed = seed
            };
        }

        [Test]
        public void Outline_StaysInsidePlaneAndReachesItsEdges()
        {
            GroundShape shape = Shape();
            float maxX = 0f, maxZ = 0f;

            for (int i = 0; i < 720; i++)
            {
                GroundMeshBuilder.OutlinePoint(shape, i * MathF.PI * 2f / 720f, out float x, out float z);
                Assert.LessOrEqual(MathF.Abs(x), shape.Width * 0.5f + 1e-4f);
                Assert.LessOrEqual(MathF.Abs(z), shape.Depth * 0.5f + 1e-4f);
                maxX = MathF.Max(maxX, MathF.Abs(x));
                maxZ = MathF.Max(maxZ, MathF.Abs(z));
            }

            Assert.GreaterOrEqual(maxX, shape.Width * 0.5f - shape.RimInset - 1e-3f);
            Assert.GreaterOrEqual(maxZ, shape.Depth * 0.5f - shape.RimInset - 1e-3f);
        }

        [Test]
        public void Build_AllVerticesStayInsidePlaneFootprint()
        {
            GroundShape shape = Shape(width: 0.45f, depth: 0.4f, jitter: 0.02f);
            GroundMeshData data = GroundMeshBuilder.Build(shape);

            for (int i = 0; i < data.VertexCount; i++)
            {
                Assert.LessOrEqual(MathF.Abs(data.Positions[i * 3]), shape.Width * 0.5f + 1e-4f);
                Assert.LessOrEqual(MathF.Abs(data.Positions[i * 3 + 2]), shape.Depth * 0.5f + 1e-4f);
                Assert.LessOrEqual(data.Positions[i * 3 + 1], shape.TopLift + shape.SurfaceJitter + 1e-4f);
            }
        }

        [Test]
        public void Build_IndicesAreValidAndTrianglesNotDegenerate()
        {
            GroundMeshData data = GroundMeshBuilder.Build(Shape(jitter: 0.02f));

            Assert.AreEqual(data.VertexCount * 2, data.Uvs.Length);
            Assert.AreEqual(0, data.TopTriangles.Length % 3);
            Assert.AreEqual(0, data.RootTriangles.Length % 3);
            Assert.AreEqual(0, data.SeamPairs.Length % 2);

            foreach (int[] triangles in new[] { data.TopTriangles, data.RootTriangles })
            {
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    for (int k = 0; k < 3; k++)
                        Assert.That(triangles[t + k], Is.InRange(0, data.VertexCount - 1));

                    Normal(data, triangles, t, out float nx, out float ny, out float nz);
                    Assert.Greater(MathF.Sqrt(nx * nx + ny * ny + nz * nz), 1e-9f, $"Triángulo degenerado en {t / 3}");
                }
            }
        }

        [Test]
        public void Build_TopFacesUpAndRootFacesOutward()
        {
            GroundMeshData data = GroundMeshBuilder.Build(Shape());

            for (int t = 0; t < data.TopTriangles.Length; t += 3)
            {
                Normal(data, data.TopTriangles, t, out _, out float ny, out _);
                Assert.Greater(ny, 0f, $"La tapa mira hacia abajo en el triángulo {t / 3}");
            }

            for (int t = 0; t < data.RootTriangles.Length; t += 3)
            {
                Normal(data, data.RootTriangles, t, out float nx, out _, out float nz);
                Centroid(data, data.RootTriangles, t, out float cx, out float cz);
                Assert.Greater(nx * cx + nz * cz, 0f, $"La raíz mira hacia dentro en el triángulo {t / 3}");
            }
        }

        [Test]
        public void Build_SameSeedIsDeterministic_DifferentSeedChangesShape()
        {
            GroundMeshData a = GroundMeshBuilder.Build(Shape(seed: 3, jitter: 0.02f));
            GroundMeshData b = GroundMeshBuilder.Build(Shape(seed: 3, jitter: 0.02f));
            GroundMeshData c = GroundMeshBuilder.Build(Shape(seed: 4, jitter: 0.02f));

            CollectionAssert.AreEqual(a.Positions, b.Positions);
            CollectionAssert.AreNotEqual(a.Positions, c.Positions);
        }

        [Test]
        public void SamplePoint_FallsInsideOutline()
        {
            GroundShape shape = Shape();
            var random = new Random(11);

            for (int i = 0; i < 500; i++)
            {
                float u = (float)random.NextDouble();
                float v = (float)random.NextDouble();
                GroundMeshBuilder.SamplePoint(shape, u, v, 0.85f, out float x, out float z);
                GroundMeshBuilder.OutlinePoint(shape, u * MathF.PI * 2f, out float rimX, out float rimZ);

                float radius = MathF.Sqrt(x * x + z * z);
                float rimRadius = MathF.Sqrt(rimX * rimX + rimZ * rimZ);
                Assert.LessOrEqual(radius, rimRadius * 0.85f + 1e-4f);
            }
        }

        [Test]
        public void Build_RejectsEmptyFootprint()
        {
            Assert.Throws<ArgumentException>(() => GroundMeshBuilder.Build(Shape(width: 0f)));
        }

        private static void Normal(GroundMeshData data, int[] triangles, int t, out float nx, out float ny, out float nz)
        {
            int a = triangles[t] * 3, b = triangles[t + 1] * 3, c = triangles[t + 2] * 3;
            float abx = data.Positions[b] - data.Positions[a];
            float aby = data.Positions[b + 1] - data.Positions[a + 1];
            float abz = data.Positions[b + 2] - data.Positions[a + 2];
            float acx = data.Positions[c] - data.Positions[a];
            float acy = data.Positions[c + 1] - data.Positions[a + 1];
            float acz = data.Positions[c + 2] - data.Positions[a + 2];

            nx = aby * acz - abz * acy;
            ny = abz * acx - abx * acz;
            nz = abx * acy - aby * acx;
        }

        private static void Centroid(GroundMeshData data, int[] triangles, int t, out float cx, out float cz)
        {
            cx = cz = 0f;
            for (int k = 0; k < 3; k++)
            {
                cx += data.Positions[triangles[t + k] * 3] / 3f;
                cz += data.Positions[triangles[t + k] * 3 + 2] / 3f;
            }
        }
    }
}
