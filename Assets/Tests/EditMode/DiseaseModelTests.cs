using System;
using System.Linq;
using NUnit.Framework;
using RiceXR.Core;

namespace RiceXR.Tests.EditMode
{
    public class DiseaseModelTests
    {
        [TestCase("3Pyri (1)", 3)]
        [TestCase("9Ryncho (2)", 9)]
        [TestCase("0Pyri (3)", 0)]
        [TestCase("6Pyri (1) G", 6)]
        public void PrefijoEsElValorReal(string mesh, int expected)
        {
            Assert.That(LeafSeverity.TryParseMeshName(mesh, out int severity), Is.True);
            Assert.That(severity, Is.EqualTo(expected));
        }

        [TestCase("3Pyri (1).base")]
        [TestCase("Pyricularia3")]
        [TestCase("10Pyri (1)")]
        [TestCase("-1Pyri")]
        [TestCase("")]
        [TestCase(null)]
        public void RechazaBasesYNombresInvalidos(string mesh) =>
            Assert.That(LeafSeverity.TryParseMeshName(mesh, out _), Is.False);

        [Test]
        public void ListaDiscontinuaComparaValoresNoPosiciones()
        {
            var spot = new DiagnosisEvaluator.Spot("ryncho", 3);
            int[] available = { 1, 3, 5, 7, 9 };
            Assert.That(DiagnosisEvaluator.MatchesAvailable(spot, "ryncho", 3, 0, available), Is.True);
            Assert.That(DiagnosisEvaluator.MatchesAvailable(spot, "ryncho", 2, 1, available), Is.False);
            Assert.That(DiagnosisEvaluator.MatchesAvailable(spot, "pyri", 3, 0, available), Is.False);
        }

        [Test]
        public void ComodinSoloAceptaValoresDisponibles()
        {
            var spot = new DiagnosisEvaluator.Spot("ryncho", -1);
            int[] available = { 1, 3, 5, 7, 9 };
            Assert.That(DiagnosisEvaluator.MatchesAvailable(spot, "ryncho", 9, 0, available), Is.True);
            Assert.That(DiagnosisEvaluator.MatchesAvailable(spot, "ryncho", 4, 0, available), Is.False);
            Assert.That(DiagnosisEvaluator.MatchesAvailable(spot, "ryncho", 0, 0, available), Is.False);
        }

        [Test]
        public void MargenEsDistanciaNumerica()
        {
            var spot = new DiagnosisEvaluator.Spot("ryncho", 3);
            int[] available = { 1, 3, 5, 7, 9 };
            Assert.That(DiagnosisEvaluator.MatchesAvailable(spot, "ryncho", 5, 1, available), Is.False);
            Assert.That(DiagnosisEvaluator.MatchesAvailable(spot, "ryncho", 5, 2, available), Is.True);
            Assert.That(DiagnosisEvaluator.MatchesAvailable(new DiagnosisEvaluator.Spot("ryncho", 4), "ryncho", 5, 2, available), Is.False);
        }

        [TestCase(0)]
        [TestCase(10)]
        [TestCase(-1)]
        public void NoAceptaRespuestaFueraDelRango(int value) =>
            Assert.That(DiagnosisEvaluator.Matches(new DiagnosisEvaluator.Spot("pyri", -1), "pyri", value, 0), Is.False);

        [Test]
        public void SpawnGarantizaObjetivoConMuchosDistractores()
        {
            var candidates = Enumerable.Repeat(false, 100).ToArray();
            candidates[73] = true;
            for (int seed = 0; seed < 100; seed++)
            {
                int[] plan = LeafSpawnPlan.Create(candidates, 20, 3, new Random(seed));
                Assert.That(plan.Length, Is.EqualTo(20));
                Assert.That(plan.Count(i => candidates[i]), Is.GreaterThanOrEqualTo(3));
            }
        }

        [Test]
        public void SpawnRechazaConfiguracionImposible()
        {
            Assert.Throws<ArgumentException>(() => LeafSpawnPlan.Create(new[] { false }, 20, 3, new Random(1)));
            Assert.Throws<ArgumentException>(() => LeafSpawnPlan.Create(new[] { true }, 2, 3, new Random(1)));
        }
    }
}
