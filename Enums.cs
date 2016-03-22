using System;

namespace PokerService.Cards
{
    public enum FlushType : byte
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3,
        Joker = 4
    }

    public enum CombinationType : byte
    {
        NONE = 0,
        PAIR = 1,
        PAIR2 = 2,
        THREE = 3,
        STRAIGHT = 4,
        FLUSH = 5,
        FULL_HOUSE = 6,
        FOUR = 7,
        STRAIGHT_FLUSH = 8,
        ROYAL_FLUSH = 9,
        POCKER = 10
    }

    public enum NumberType : byte
    {
        _2,
        _3,
        _4,
        _5,
        _6,
        _7,
        _8,
        _9,
        _10,
        J,
        Q,
        K,
        A
    }
}
