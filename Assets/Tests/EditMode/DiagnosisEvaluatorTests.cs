using NUnit.Framework;
using System.Collections.Generic;
using RiceXR.Core;

namespace RiceXR.Tests.EditMode
{
    /// <summary>
    /// Cubre la lógica pura de validación de diagnóstico (severidad, margen y comodín -1).
    /// Es la misma lógica que usa DiseaseSelectionSystem.ValidateSelection en producción.
    /// </summary>
    public class DiagnosisEvaluatorTests
    {
        private const string DiseaseA = "Pyricularia oryzae";
        private const string DiseaseB = "Rhynchosporium oryzae";

        [Test]
        public void Matches_ExactDiseaseAndSeverity_NoMargin_ReturnsTrue()
        {
            var spot = new DiagnosisEvaluator.Spot(DiseaseA, 3);
            Assert.IsTrue(DiagnosisEvaluator.Matches(spot, DiseaseA, 3, marginOfError: 0));
        }

        [Test]
        public void Matches_WrongDisease_ReturnsFalse()
        {
            var spot = new DiagnosisEvaluator.Spot(DiseaseA, 3);
            Assert.IsFalse(DiagnosisEvaluator.Matches(spot, DiseaseB, 3, marginOfError: 0));
        }

        [Test]
        public void Matches_SeverityOffByOne_NoMargin_ReturnsFalse()
        {
            var spot = new DiagnosisEvaluator.Spot(DiseaseA, 3);
            Assert.IsFalse(DiagnosisEvaluator.Matches(spot, DiseaseA, 4, marginOfError: 0));
        }

        [Test]
        public void Matches_SeverityWithinMargin_ReturnsTrue()
        {
            var spot = new DiagnosisEvaluator.Spot(DiseaseA, 3);
            Assert.IsTrue(DiagnosisEvaluator.Matches(spot, DiseaseA, 4, marginOfError: 1));
            Assert.IsTrue(DiagnosisEvaluator.Matches(spot, DiseaseA, 2, marginOfError: 1));
        }

        [Test]
        public void Matches_SeverityOutsideMargin_ReturnsFalse()
        {
            var spot = new DiagnosisEvaluator.Spot(DiseaseA, 3);
            Assert.IsFalse(DiagnosisEvaluator.Matches(spot, DiseaseA, 5, marginOfError: 1));
        }

        [Test]
        public void Matches_WildcardSeverity_IgnoresSeverity_ReturnsTrue()
        {
            var spot = new DiagnosisEvaluator.Spot(DiseaseA, -1);
            Assert.IsTrue(DiagnosisEvaluator.Matches(spot, DiseaseA, 1, marginOfError: 0));
            Assert.IsTrue(DiagnosisEvaluator.Matches(spot, DiseaseA, 5, marginOfError: 0));
        }

        [Test]
        public void Matches_WildcardSeverity_StillChecksDisease_ReturnsFalse()
        {
            var spot = new DiagnosisEvaluator.Spot(DiseaseA, -1);
            Assert.IsFalse(DiagnosisEvaluator.Matches(spot, DiseaseB, 3, marginOfError: 0));
        }

        [Test]
        public void IsCorrect_MatchesOneOfSeveralSpots_ReturnsTrue()
        {
            var spots = new List<DiagnosisEvaluator.Spot>
            {
                new DiagnosisEvaluator.Spot(DiseaseA, 2),
                new DiagnosisEvaluator.Spot(DiseaseB, 4),
            };
            Assert.IsTrue(DiagnosisEvaluator.IsCorrect(spots, DiseaseB, 4, marginOfError: 0));
        }

        [Test]
        public void IsCorrect_NoSpotMatches_ReturnsFalse()
        {
            var spots = new List<DiagnosisEvaluator.Spot>
            {
                new DiagnosisEvaluator.Spot(DiseaseA, 2),
                new DiagnosisEvaluator.Spot(DiseaseB, 4),
            };
            Assert.IsFalse(DiagnosisEvaluator.IsCorrect(spots, DiseaseA, 5, marginOfError: 0));
        }

        [Test]
        public void IsCorrect_EmptyList_ReturnsFalse()
        {
            var spots = new List<DiagnosisEvaluator.Spot>();
            Assert.IsFalse(DiagnosisEvaluator.IsCorrect(spots, DiseaseA, 3, marginOfError: 0));
        }

        [Test]
        public void IsCorrect_NullList_ReturnsFalse()
        {
            Assert.IsFalse(DiagnosisEvaluator.IsCorrect(null, DiseaseA, 3, marginOfError: 0));
        }
    }
}
