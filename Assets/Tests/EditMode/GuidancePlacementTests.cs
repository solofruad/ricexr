using NUnit.Framework;
using RiceXR.Core;

namespace RiceXR.Tests.EditMode
{
    /// <summary>
    /// Cubre la colocación de paneles de guía: nunca dentro del volumen de juego ampliado, sin cambios
    /// cuando el jugador está lejos de la mesa y siempre dentro del rango de distancias.
    /// </summary>
    public class GuidancePlacementTests
    {
        private const float Margin = 0.15f;

        private static GuidanceVolume Table(float yaw = 0f)
        {
            return new GuidanceVolume
            {
                SurfaceCenter = new GuidancePoint(0f, 0.75f, 0f),
                YawDegrees = yaw,
                SizeX = 1.2f,
                SizeZ = 0.7f,
                LeafHeight = 0.9f
            };
        }

        private static GuidancePlacementOptions Options()
        {
            return new GuidancePlacementOptions
            {
                Distance = 1.2f,
                HeightOffset = -0.15f,
                Margin = Margin,
                MinDistance = 0.6f,
                MaxDistance = 2.5f
            };
        }

        private static float Distance(GuidancePoint a, GuidancePoint b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static GuidancePoint LookAtTable(GuidancePoint head, GuidanceVolume volume)
        {
            return new GuidancePoint(
                volume.SurfaceCenter.X - head.X,
                volume.SurfaceCenter.Y - head.Y,
                volume.SurfaceCenter.Z - head.Z);
        }

        [TestCase(0f, 0f, 0.9f, 0f)]
        [TestCase(0f, 0f, 1.2f, 30f)]
        [TestCase(0f, 0f, -0.9f, 90f)]
        [TestCase(0f, 0f, 1.6f, 45f)]
        [TestCase(1.0f, 0f, 0.8f, 0f)]
        [TestCase(-1.1f, 0f, 0.3f, 60f)]
        [TestCase(0.2f, 0f, -1.4f, 135f)]
        public void Resolve_HeadLookingAtTable_PanelStaysOutsideExpandedVolume(float hx, float hy, float hz, float yaw)
        {
            GuidanceVolume volume = Table(yaw);
            var head = new GuidancePoint(hx, 1.6f + hy, hz);

            GuidancePoint panel = GuidancePlacement.Resolve(head, LookAtTable(head, volume), volume, Options());

            Assert.IsFalse(GuidancePlacement.IsInsideVolume(panel, volume, Margin));
        }

        [Test]
        public void Resolve_HeadLookingStraightDownAtTable_PanelStaysOutsideExpandedVolume()
        {
            GuidanceVolume volume = Table();
            var head = new GuidancePoint(0.1f, 1.7f, 0.05f);
            var down = new GuidancePoint(0f, -1f, 0f);

            GuidancePoint panel = GuidancePlacement.Resolve(head, down, volume, Options());

            Assert.IsFalse(GuidancePlacement.IsInsideVolume(panel, volume, Margin));
        }

        [Test]
        public void Resolve_HeadFarFromTable_KeepsPlainForwardPlacement()
        {
            GuidanceVolume volume = Table();
            var head = new GuidancePoint(0f, 1.6f, 4f);
            var forward = new GuidancePoint(0f, 0f, 1f);

            GuidancePoint panel = GuidancePlacement.Resolve(head, forward, volume, Options());

            Assert.AreEqual(0f, panel.X, 1e-4f);
            Assert.AreEqual(1.45f, panel.Y, 1e-4f);
            Assert.AreEqual(5.2f, panel.Z, 1e-4f);
        }

        [Test]
        public void Resolve_HeadLookingAwayFromTable_KeepsPlainForwardPlacement()
        {
            GuidanceVolume volume = Table();
            var head = new GuidancePoint(0f, 1.6f, 1f);
            var forward = new GuidancePoint(0f, 0f, 1f);

            GuidancePoint panel = GuidancePlacement.Resolve(head, forward, volume, Options());

            Assert.AreEqual(2.2f, panel.Z, 1e-4f);
        }

        [TestCase(0f, 0.9f, 0f)]
        [TestCase(0.5f, 1.2f, 40f)]
        [TestCase(-0.8f, -1.0f, 100f)]
        [TestCase(0f, 1.6f, 0f)]
        public void Resolve_HeadNearTable_StaysWithinDistanceBounds(float hx, float hz, float yaw)
        {
            GuidanceVolume volume = Table(yaw);
            GuidancePlacementOptions options = Options();
            var head = new GuidancePoint(hx, 1.6f, hz);

            GuidancePoint panel = GuidancePlacement.Resolve(head, LookAtTable(head, volume), volume, options);

            float distance = Distance(head, panel);
            Assert.GreaterOrEqual(distance, options.MinDistance);
            Assert.LessOrEqual(distance, options.MaxDistance);
        }
    }
}
