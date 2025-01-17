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
                new Card(Suit.Copas, Value.Five),   // Matching card
                new Card(Suit.Bastos, Value.Six),   // Part of sequence
                new Card(Suit.Espadas, Value.Seven), // Part of sequence
                new Card(Suit.Oros, Value.Ten),     // Not part of sequence
                new Card(Suit.Copas, Value.Two)     // Not part of sequence
            });

            var captureableCards = Rules.GetCaptureableCards(playedCard, _tableCards);

            Assert.AreEqual(4, captureableCards.Count);
            Assert.IsTrue(captureableCards.Any(c => c.Value == Value.Five));
            Assert.IsTrue(captureableCards.Any(c => c.Value == Value.Six));
            Assert.IsTrue(captureableCards.Any(c => c.Value == Value.Seven));
            Assert.IsTrue(captureableCards.Any(c => c.Value == Value.Ten));
        }

        [Test]
        public void GetCaptureableCards_WithMultipleMatchingCards_ReturnsAllMatches()
        {
            var playedCard = new Card(Suit.Oros, Value.Five);
            _tableCards.AddRange(new[]
            {
                new Card(Suit.Copas, Value.Five),
                new Card(Suit.Bastos, Value.Five),
                new Card(Suit.Espadas, Value.Seven)
            });

            var captureableCards = Rules.GetCaptureableCards(playedCard, _tableCards);

            Assert.AreEqual(2, captureableCards.Count);
            Assert.AreEqual(2, captureableCards.Count(c => c.Value == Value.Five));
        }

        [Test]
        public void CalculateCapturePoints_WithBasicMatch_ReturnsTwoPoints()
        {
            var capturedCards = new List<Card>
            {
                new Card(Suit.Oros, Value.Five),
                new Card(Suit.Copas, Value.Five)
            };

            var points = Rules.CalculateCapturePoints(capturedCards, false);

            Assert.AreEqual(2, points); // Base points for match
        }

        [Test]
        public void CalculateCapturePoints_WithSequence_ReturnsCorrectPoints()
        {
            var capturedCards = new List<Card>
            {
                new Card(Suit.Oros, Value.Five),
                new Card(Suit.Copas, Value.Six),
                new Card(Suit.Bastos, Value.Seven)
            };

            var points = Rules.CalculateCapturePoints(capturedCards, false);

            Assert.AreEqual(4, points); // 2 base points + 2 for sequence length
        }

        [Test]
        public void CalculateCapturePoints_WithLongerSequence_ReturnsCorrectPoints()
        {
            var capturedCards = new List<Card>
            {
                new Card(Suit.Oros, Value.Four),
                new Card(Suit.Copas, Value.Five),
                new Card(Suit.Bastos, Value.Six),
                new Card(Suit.Espadas, Value.Seven)
            };

            var points = Rules.CalculateCapturePoints(capturedCards, false);

            Assert.AreEqual(5, points); // 2 base points + 3 for sequence length
        }

        [Test]
        public void GetCaptureableCards_WithNoSequence_ReturnsOnlyMatchingCard()
        {
            var playedCard = new Card(Suit.Oros, Value.Five);
            _tableCards.AddRange(new[]
            {
                new Card(Suit.Copas, Value.Five),
                new Card(Suit.Bastos, Value.Seven),
                new Card(Suit.Espadas, Value.Ten)
            });

            var captureableCards = Rules.GetCaptureableCards(playedCard, _tableCards);

            Assert.AreEqual(1, captureableCards.Count);
            Assert.IsTrue(captureableCards.All(c => c.Value == Value.Five));
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