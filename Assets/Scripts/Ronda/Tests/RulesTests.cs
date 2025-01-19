using System.Collections.Generic;
using System.Linq;
using KKL.Ronda.Core;
using NUnit.Framework;

namespace KKL.Ronda.Tests
{
    [TestFixture]
    public class RulesTests
    {
        [Test]
        public void AreValidTableCards_ValidCards_ReturnsTrue()
        {
            // Arrange
            var tableCards = new List<Card>
            {
                new(Suit.Oros, Value.One),
                new(Suit.Bastos, Value.Three),
                new(Suit.Espadas, Value.Five),
                new(Suit.Copas, Value.Seven)
            };

            // Act
            var result = Rules.AreValidTableCards(tableCards);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void AreValidTableCards_IncorrectCount_ReturnsFalse()
        {
            // Arrange
            var tableCards = new List<Card>
            {
                new(Suit.Oros, Value.One),
                new(Suit.Bastos, Value.Three),
                new(Suit.Espadas, Value.Five)
            };

            // Act
            var result = Rules.AreValidTableCards(tableCards);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void AreValidTableCards_ContainsPair_ReturnsFalse()
        {
            // Arrange
            var tableCards = new List<Card>
            {
                new(Suit.Oros, Value.One),
                new(Suit.Bastos, Value.One),
                new(Suit.Espadas, Value.Five),
                new(Suit.Copas, Value.Seven)
            };

            // Act
            var result = Rules.AreValidTableCards(tableCards);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void AreValidTableCards_ContainsSequence_ReturnsFalse()
        {
            // Arrange
            var tableCards = new List<Card>
            {
                new(Suit.Oros, Value.One),
                new(Suit.Bastos, Value.Two),
                new(Suit.Espadas, Value.Five),
                new(Suit.Copas, Value.Seven)
            };

            // Act
            var result = Rules.AreValidTableCards(tableCards);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void HasRonda_ContainsPair_ReturnsTrue()
        {
            // Arrange
            var handCards = new List<Card>
            {
                new(Suit.Oros, Value.One),
                new(Suit.Bastos, Value.One),
                new(Suit.Espadas, Value.Five)
            };

            // Act
            var result = Rules.HasRonda(handCards);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void HasRonda_NoPair_ReturnsFalse()
        {
            // Arrange
            var handCards = new List<Card>
            {
                new(Suit.Oros, Value.One),
                new(Suit.Bastos, Value.Two),
                new(Suit.Espadas, Value.Five)
            };

            // Act
            var result = Rules.HasRonda(handCards);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void HasTringa_ContainsThreeOfAKind_ReturnsTrue()
        {
            // Arrange
            var handCards = new List<Card>
            {
                new(Suit.Oros, Value.One),
                new(Suit.Bastos, Value.One),
                new(Suit.Espadas, Value.One)
            };

            // Act
            var result = Rules.HasTringa(handCards);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void HasTringa_NoThreeOfAKind_ReturnsFalse()
        {
            // Arrange
            var handCards = new List<Card>
            {
                new(Suit.Oros, Value.One),
                new(Suit.Bastos, Value.One),
                new(Suit.Espadas, Value.Five)
            };

            // Act
            var result = Rules.HasTringa(handCards);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void GetHighestRondaValue_MultipleRondas_ReturnsHighestValue()
        {
            // Arrange
            var handCards = new List<Card>
            {
                new(Suit.Oros, Value.One),
                new(Suit.Bastos, Value.One),
                new(Suit.Oros, Value.Seven),
                new(Suit.Bastos, Value.Seven)
            };

            // Act
            var result = Rules.GetHighestRondaValue(handCards);

            // Assert
            Assert.That(result, Is.EqualTo(Value.Seven));
        }
        
        [Test]
        public void GetHighestTringaValue_MultipleTringas_ReturnsHighestValue()
        {
            // Arrange
            var handCards = new List<Card>
            {
                new(Suit.Copas, Value.One),
                new(Suit.Oros, Value.One),
                new(Suit.Bastos, Value.One),
                new(Suit.Copas, Value.Six),
                new(Suit.Oros, Value.Six),
                new(Suit.Bastos, Value.Six)
            };

            // Act
            var result = Rules.GetHighestTringaValue(handCards);

            // Assert
            Assert.That(result, Is.EqualTo(Value.Six));
        }


        [Test]
        public void CanCapture_MatchingCard_ReturnsTrue()
        {
            // Arrange
            var playedCard = new Card(Suit.Oros, Value.One);
            var tableCards = new List<Card>
            {
                new(Suit.Bastos, Value.One),
                new(Suit.Espadas, Value.Five)
            };

            // Act
            var result = Rules.CanCapture(playedCard, tableCards);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void CanCapture_NoMatchingCard_ReturnsFalse()
        {
            // Arrange
            var playedCard = new Card(Suit.Oros, Value.One);
            var tableCards = new List<Card>
            {
                new(Suit.Bastos, Value.Two),
                new(Suit.Espadas, Value.Five)
            };

            // Act
            var result = Rules.CanCapture(playedCard, tableCards);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void GetMandatoryCaptureCards_SingleMatch_ReturnsMatchingCard()
        {
            // Arrange
            var playedCard = new Card(Suit.Oros, Value.One);
            var tableCards = new List<Card>
            {
                new(Suit.Bastos, Value.One),
                new(Suit.Espadas, Value.Five)
            };

            // Act
            var result = Rules.GetMandatoryCaptureCards(playedCard, tableCards);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Value, Is.EqualTo(Value.One));
        }

        [Test]
        public void GetMandatoryCaptureCards_MatchWithSequence_ReturnsAllCards()
        {
            // Arrange
            var playedCard = new Card(Suit.Oros, Value.One);
            var tableCards = new List<Card>
            {
                new(Suit.Bastos, Value.One),
                new(Suit.Espadas, Value.Two),
                new(Suit.Copas, Value.Three)
            };

            // Act
            var result = Rules.GetMandatoryCaptureCards(playedCard, tableCards);

            // Assert
            Assert.That(result.Count, Is.EqualTo(4));
            Assert.That(result.Select(c => c.Value), Is.EquivalentTo(new[] 
                { Value.One, Value.One, Value.Two, Value.Three }));
        }
        
        [Test]
        public void CalculateExtraCardPoints_MoreCards_ReturnsCorrectPoints()
        {
            // Act & Assert
            Assert.That(Rules.CalculateExtraCardPoints(25, 15), Is.EqualTo(10));
            Assert.That(Rules.CalculateExtraCardPoints(35, 5), Is.EqualTo(20)); // Max points
            Assert.That(Rules.CalculateExtraCardPoints(15, 15), Is.EqualTo(0)); // Equal cards
            Assert.That(Rules.CalculateExtraCardPoints(10, 15), Is.EqualTo(0)); // Fewer cards
        }

        [Test]
        public void IsGameOver_ScoreReachesWinningScore_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Rules.IsGameOver(41));
            Assert.IsTrue(Rules.IsGameOver(45));
            Assert.IsFalse(Rules.IsGameOver(40));
            Assert.IsFalse(Rules.IsGameOver(0));
        }

        [Test]
        public void IsValidDeck_CorrectDeck_ReturnsTrue()
        {
            // Arrange
            var deck = new Deck(); // Creates a standard deck

            // Act
            var result = Rules.IsValidDeck(deck);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsValidDeck_DuplicateCards_ReturnsFalse()
        {
            // Arrange
            var cards = new List<int>
            {
                101, 101 // Two Oros One cards
            };
            var deck = new Deck(cards);

            // Act
            var result = Rules.IsValidDeck(deck);

            // Assert
            Assert.IsFalse(result);
        }
    }
}