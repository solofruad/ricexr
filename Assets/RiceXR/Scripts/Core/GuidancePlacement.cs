using System;

namespace RiceXR.Core
{
    /// <summary>Punto en el espacio del mundo, en metros. Tipo propio porque RiceXR.Core no referencia el motor.</summary>
    public struct GuidancePoint
    {
        public float X;
        public float Y;
        public float Z;

        public GuidancePoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Volumen de juego: la caja orientada que ocupan la superficie seleccionada y las hojas sobre ella.
    /// </summary>
    public struct GuidanceVolume
    {
        /// <summary>Centro de la superficie de juego (donde nacen las hojas).</summary>
        public GuidancePoint SurfaceCenter;
        /// <summary>Giro de la superficie alrededor del eje vertical, en grados (convención de Unity).</summary>
        public float YawDegrees;
        /// <summary>Ancho (X local) de la superficie, en metros.</summary>
        public float SizeX;
        /// <summary>Fondo (Z local) de la superficie, en metros.</summary>
        public float SizeZ;
        /// <summary>Altura que alcanzan las hojas sobre la superficie, en metros.</summary>
        public float LeafHeight;
    }

    /// <summary>
    /// Parámetros de colocación de un panel de guía.
    /// </summary>
    public struct GuidancePlacementOptions
    {
        /// <summary>Distancia horizontal deseada entre la cabeza y el panel.</summary>
        public float Distance;
        /// <summary>Desplazamiento vertical respecto a la altura de la cabeza.</summary>
        public float HeightOffset;
        /// <summary>Holgura que se suma al volumen de juego en todas las direcciones.</summary>
        public float Margin;
        /// <summary>Distancia mínima admitida entre la cabeza y el panel.</summary>
        public float MinDistance;
        /// <summary>Distancia máxima admitida entre la cabeza y el panel.</summary>
        public float MaxDistance;
    }

    /// <summary>
    /// Coloca los paneles de guía fuera del volumen de juego para que nunca queden sobre las hojas.
    ///
    /// Estrategia:
    /// 1. Candidato: delante de la cabeza, a la distancia pedida y con el desplazamiento vertical.
    /// 2. Si el segmento cabeza-candidato no toca el volumen ampliado por el margen, se conserva tal cual.
    /// 3. Si lo toca (el panel quedaría dentro, o entre la cabeza y las hojas), se coloca detrás del borde
    ///    lejano del volumen visto desde la cabeza, a la altura de la punta de las hojas más el margen.
    /// 4. Alternativa: el candidato elevado por encima del volumen. Entre las dos opciones se elige la que
    ///    respete el rango de distancias o, si ninguna lo respeta, la más cercana a él.
    /// Ambas opciones quedan siempre fuera del volumen ampliado.
    /// </summary>
    public static class GuidancePlacement
    {
        private const float Epsilon = 1e-5f;

        /// <summary>Separación extra sobre el borde del volumen ampliado, para quedar estrictamente fuera.</summary>
        private const float Clearance = 0.02f;

        /// <summary>
        /// Calcula la posición del panel. <paramref name="forward"/> es la dirección de la mirada; solo se usa
        /// su proyección horizontal (con respaldo al eje Z si la mirada es vertical).
        /// </summary>
        public static GuidancePoint Resolve(GuidancePoint head, GuidancePoint forward,
            GuidanceVolume volume, GuidancePlacementOptions options)
        {
            Horizontal(forward.X, forward.Z, 0f, 1f, out float fx, out float fz);

            var candidate = new GuidancePoint(
                head.X + fx * options.Distance,
                head.Y + options.HeightOffset,
                head.Z + fz * options.Distance);

            if (!SegmentTouchesVolume(head, candidate, volume, options.Margin))
                return candidate;

            GuidancePoint behind = PlaceBehindVolume(head, fx, fz, volume, options.Margin);
            GuidancePoint above = candidate;
            above.Y = TopOf(volume) + options.Margin + Clearance;

            float behindError = DistanceError(head, behind, options);
            float aboveError = DistanceError(head, above, options);
            return behindError <= aboveError ? behind : above;
        }

        /// <summary>
        /// Indica si un punto está dentro del volumen de juego ampliado por el margen.
        /// </summary>
        public static bool IsInsideVolume(GuidancePoint point, GuidanceVolume volume, float margin)
        {
            ToLocal(point, volume, out float lx, out float lz);
            return Math.Abs(lx) <= volume.SizeX * 0.5f + margin
                && Math.Abs(lz) <= volume.SizeZ * 0.5f + margin
                && point.Y >= volume.SurfaceCenter.Y - margin
                && point.Y <= TopOf(volume) + margin;
        }

        private static float TopOf(GuidanceVolume volume)
        {
            return volume.SurfaceCenter.Y + volume.LeafHeight;
        }

        private static float DistanceError(GuidancePoint head, GuidancePoint point, GuidancePlacementOptions options)
        {
            float distance = Length(point.X - head.X, point.Y - head.Y, point.Z - head.Z);
            if (distance < options.MinDistance) return options.MinDistance - distance;
            if (distance > options.MaxDistance) return distance - options.MaxDistance;
            return 0f;
        }

        /// <summary>
        /// Posición tras el borde lejano del volumen, siguiendo la dirección horizontal cabeza-centro.
        /// </summary>
        private static GuidancePoint PlaceBehindVolume(GuidancePoint head, float fallbackX, float fallbackZ,
            GuidanceVolume volume, float margin)
        {
            Horizontal(volume.SurfaceCenter.X - head.X, volume.SurfaceCenter.Z - head.Z,
                fallbackX, fallbackZ, out float dx, out float dz);

            ToLocal(head, volume, out float ox, out float oz);
            Rotate(dx, dz, volume.YawDegrees, out float ldx, out float ldz);

            float halfX = volume.SizeX * 0.5f + margin;
            float halfZ = volume.SizeZ * 0.5f + margin;

            float exit = float.MaxValue;
            if (Math.Abs(ldx) > Epsilon) exit = Math.Min(exit, ((ldx > 0f ? halfX : -halfX) - ox) / ldx);
            if (Math.Abs(ldz) > Epsilon) exit = Math.Min(exit, ((ldz > 0f ? halfZ : -halfZ) - oz) / ldz);
            if (exit == float.MaxValue || exit < 0f) exit = 0f;

            float travel = exit + margin;
            return new GuidancePoint(head.X + dx * travel, TopOf(volume) + margin + Clearance, head.Z + dz * travel);
        }

        /// <summary>
        /// Prueba segmento-caja con el método de las losas, en el espacio local del volumen ampliado.
        /// </summary>
        private static bool SegmentTouchesVolume(GuidancePoint from, GuidancePoint to, GuidanceVolume volume, float margin)
        {
            ToLocal(from, volume, out float ax, out float az);
            ToLocal(to, volume, out float bx, out float bz);

            float halfX = volume.SizeX * 0.5f + margin;
            float halfZ = volume.SizeZ * 0.5f + margin;

            float tMin = 0f;
            float tMax = 1f;
            return ClipAxis(ax, bx, -halfX, halfX, ref tMin, ref tMax)
                && ClipAxis(from.Y, to.Y, volume.SurfaceCenter.Y - margin, TopOf(volume) + margin, ref tMin, ref tMax)
                && ClipAxis(az, bz, -halfZ, halfZ, ref tMin, ref tMax);
        }

        private static bool ClipAxis(float a, float b, float min, float max, ref float tMin, ref float tMax)
        {
            float delta = b - a;
            if (Math.Abs(delta) < Epsilon)
                return a >= min && a <= max;

            float t1 = (min - a) / delta;
            float t2 = (max - a) / delta;
            if (t1 > t2)
            {
                float swap = t1;
                t1 = t2;
                t2 = swap;
            }

            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            return tMin <= tMax;
        }

        /// <summary>Pasa un punto al plano horizontal local del volumen (relativo al centro y girado por el yaw).</summary>
        private static void ToLocal(GuidancePoint point, GuidanceVolume volume, out float lx, out float lz)
        {
            Rotate(point.X - volume.SurfaceCenter.X, point.Z - volume.SurfaceCenter.Z, volume.YawDegrees, out lx, out lz);
        }

        private static void Rotate(float x, float z, float yawDegrees, out float lx, out float lz)
        {
            double radians = yawDegrees * Math.PI / 180.0;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            lx = x * cos - z * sin;
            lz = x * sin + z * cos;
        }

        private static void Horizontal(float x, float z, float fallbackX, float fallbackZ, out float nx, out float nz)
        {
            float length = (float)Math.Sqrt(x * x + z * z);
            if (length < 1e-4f)
            {
                nx = fallbackX;
                nz = fallbackZ;
                return;
            }

            nx = x / length;
            nz = z / length;
        }

        private static float Length(float x, float y, float z)
        {
            return (float)Math.Sqrt(x * x + y * y + z * z);
        }
    }
}
