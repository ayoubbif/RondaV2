using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using KKL.Ronda.Core;

namespace KKL.Ronda.Tests
{
    [TestFixture]
    public class RulesTests
    {
        private List<Card> _tableCards;

        [SetUp]
        public void Setup()
        {
            _tableCards = new List<Card>();
        }

        [Test]
        public void CanCapture_WithMatchingCard_ReturnsTrue()
        {
            var playedCard = new Card(Suit.Oros, Value.Seven);
            _tableCards.Add(new Card(Suit.Copas, Value.Seven));

            var result = Rules.CanCapture(playedCard, _tableCards);

            Assert.IsTrue(result);
        }

        [Test]
        public void CanCapture_WithNoMatchingCard_ReturnsFalse()
        {
            var playedCard = new Card(Suit.Oros, Value.Seven);
            _tableCards.Add(new Card(Suit.Copas, Value.Six));

            var result = Rules.CanCapture(playedCard, _tableCards);

            Assert.IsFalse(result);
        }

        [Test]
        public void GetCaptureableCards_WithSequence_ReturnsCorrectCards()
        {
            var playedCard = new Card(Suit.Oros, Value.Five);
            _tableCards.AddRange(new[]
            {
                new Card(Suit.Copas, Value.Five),
                new Card(Suit.Bastos, Value.Six),
                new Card(Suit.Espadas, Value.Seven),
                new Card(Suit.Oros, Value.Ten),
                new Card(Suit.Copas, Value.Two)
            });

            var captureableCards = Rules.GetCaptureableCards(playedCard, _tableCards);

            Assert.AreEqual(3, captureableCards.Count);
            Assert.IsTrue(captureableCards.Any(c => c.Value == Value.Five));
            Assert.IsTrue(captureableCards.Any(c => c.Value == Value.Six));
            Assert.IsTrue(captureableCards.Any(c => c.Value == Value.Seven));
            Assert.IsFalse(captureableCards.Any(c => c.Value == Value.Ten));
        }

        [Test]
        public void CalculateCapturePoints_WithWahad_ReturnsCorrectPoints()
        {
            var capturedCards = new List<Card>
            {
                new Card(Suit.Oros, Value.One),
                new Card(Suit.Copas, Value.Two)
            };

            var points = Rules.CalculateCapturePoints(capturedCards, false);

            Assert.AreEqual(3, points); // 2 cards + 1 Wahad bonus
        }

        [Test]
        public void CalculateCapturePoints_WithKhamsa_ReturnsCorrectPoints()
        {
            var capturedCards = new List<Card>
            {
                new(Suit.Oros, Value.Five),
                new Card(Suit.Copas, Value.Six)
            };

            var points = Rules.CalculateCapturePoints(capturedCards, false);

            Assert.AreEqual(7, points); // 2 cards + 5 Khamsa bonus
        }

        [Test]
        public void CalculateCapturePoints_WithAshra_ReturnsCorrectPoints()
        {
            var capturedCards = new List<Card>
            {
                new(Suit.Oros, Value.Ten),
                new(Suit.Copas, Value.Two)
            };

            var points = Rules.CalculateCapturePoints(capturedCards, false);

            Assert.AreEqual(12, points); // 2 cards + 10 Ashra bonus
        }

        [Test]
        public void CalculateCapturePoints_WithMissaSequence_ReturnsCorrectPoints()
        {
            var capturedCards = new List<Card>
            {
                new(Suit.Oros, Value.Five),
                new(Suit.Copas, Value.Six),
                new(Suit.Bastos, Value.Seven)
            };

            var points = Rules.CalculateCapturePoints(capturedCards, false);

            Assert.AreEqual(4, points); // 3 cards + 1 Missa bonus
        }

        [Test]
        public void HasValidMissaSequence_WithSequencePastSeven_ReturnsFalse()
        {
            var cards = new List<Card>
            {
                new(Suit.Oros, Value.Seven),
                new(Suit.Copas, Value.Ten),
                new(Suit.Bastos, Value.Eleven)
            };

            var result = Rules.HasValidMissaSequence(cards);

            Assert.IsFalse(result);
        }

        [Test]
        public void CalculateCapturePoints_WithFinalThrow_ReturnsCorrectPoints()
        {
            var capturedCards = new List<Card>
            {
                new(Suit.Oros, Value.Twelve),
                new(Suit.Copas, Value.Two)
            };

            var points = Rules.CalculateCapturePoints(capturedCards, true);

            Assert.AreEqual(7, points); // 2 cards + 5 final throw bonus
        }

        [Test]
        public void IsValidDeck_WithCorrectDeck_ReturnsTrue()
        {
            var deck = new Deck();

            var result = Rules.IsValidDeck(deck);

            Assert.IsTrue(result);
        }

        [Test]
        public void IsValidDeck_WithModifiedDeck_ReturnsFalse()
        {
            var deck = new Deck();
            _ = deck.PullCard();

            var result = Rules.IsValidDeck(deck);

            Assert.IsFalse(result);
        }
    }
}