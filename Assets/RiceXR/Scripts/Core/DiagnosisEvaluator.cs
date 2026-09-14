using System;
using System.Collections.Generic;

namespace RiceXR.Core
{
    /// <summary>
    /// Lógica pura de validación de diagnóstico, extraída de DiseaseSelectionSystem
    /// para poder testearla en EditMode sin depender de tipos de escena (MonoBehaviour).
    /// Trabaja únicamente sobre datos, por lo que vive en su propio assembly (RiceXR.Core).
    /// </summary>
    public static class DiagnosisEvaluator
    {
        /// <summary>
        /// Una mancha candidata: la enfermedad esperada y su severidad.
        /// Severity == -1 significa "cualquier severidad" (comodín).
        /// </summary>
        public readonly struct Spot
        {
            public readonly string DiseaseName;
            public readonly int Severity;

            public Spot(string diseaseName, int severity)
            {
                DiseaseName = diseaseName;
                Severity = severity;
            }
        }

        /// <summary>
        /// Devuelve true si la selección (enfermedad + severidad) coincide con la mancha.
        /// La enfermedad debe coincidir exactamente; la severidad coincide si la mancha
        /// es comodín (Severity == -1) o si la diferencia está dentro del margen.
        /// </summary>
        public static bool Matches(Spot spot, string selectedDisease, int selectedSeverity, int marginOfError)
        {
            bool diseaseMatch = !string.IsNullOrWhiteSpace(selectedDisease) && selectedDisease == spot.DiseaseName;
            if (selectedSeverity < 1 || selectedSeverity > 9 || marginOfError < 0
                || (spot.Severity != -1 && (spot.Severity < 1 || spot.Severity > 9))) return false;
            bool severityMatch = spot.Severity == -1
                || Math.Abs(selectedSeverity - spot.Severity) <= marginOfError;
            return diseaseMatch && severityMatch;
        }

        public static bool MatchesAvailable(Spot spot, string selectedDisease, int selectedSeverity,
            int marginOfError, IEnumerable<int> availableSeverities)
        {
            if (availableSeverities == null) return false;
            bool selectedAvailable = false;
            bool expectedAvailable = spot.Severity == -1;
            foreach (int severity in availableSeverities)
            {
                if (severity == selectedSeverity) selectedAvailable = true;
                if (severity == spot.Severity) expectedAvailable = true;
            }
            return selectedAvailable && expectedAvailable && Matches(spot, selectedDisease, selectedSeverity, marginOfError);
        }

        /// <summary>
        /// Devuelve true si la selección coincide con ALGUNA de las manchas.
        /// </summary>
        public static bool IsCorrect(IEnumerable<Spot> spots, string selectedDisease, int selectedSeverity, int marginOfError)
        {
            if (spots == null) return false;
            foreach (var spot in spots)
            {
                if (Matches(spot, selectedDisease, selectedSeverity, marginOfError))
                    return true;
            }
            return false;
        }
    }
}
