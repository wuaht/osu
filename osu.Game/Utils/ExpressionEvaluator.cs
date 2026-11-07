// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace osu.Game.Utils
{
    /// <summary>
    /// Evaluates simple arithmetic expressions typed into numeric inputs (like osu!stable's editor expression text boxes).
    /// Supports <c>+ - * / %</c>, parentheses, unary signs and case-insensitive math functions such as <c>Sqrt(2)</c> or <c>Pow(2, 3)</c>.
    /// </summary>
    public static class ExpressionEvaluator
    {
        /// <summary>
        /// Attempts to evaluate <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The expression.</param>
        /// <param name="angleMode">
        /// Whether to interpret the expression as an angle in degrees, like osu!stable's rotate dialog:
        /// a leading <c>/</c> is shorthand for <c>1/</c>, and non-literal expressions evaluating to a fraction of a full turn are multiplied by 360
        /// (so <c>/5</c> and <c>1/5</c> both result in 72).
        /// </param>
        /// <param name="result">The evaluated value.</param>
        /// <param name="isLiteral">Whether the expression is a plain (optionally negated) number rather than a calculation.</param>
        /// <returns>Whether the expression was valid and evaluated to a finite number.</returns>
        public static bool TryEvaluate(string text, bool angleMode, out double result, out bool isLiteral)
        {
            result = 0;
            isLiteral = false;

            text = text.Trim();

            if (text.Length == 0)
                return false;

            // keeps plain numbers in the user's number format working (e.g. "1,5" with a German locale).
            // a leading "+" is excluded as osu!stable doesn't consider it a literal.
            if (!text.StartsWith('+') && double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
            {
                isLiteral = true;
                return double.IsFinite(result);
            }

            if (angleMode && text.StartsWith('/'))
                text = "1" + text;

            var parser = new Parser(text);

            if (!parser.TryParse(out var node))
            {
                result = 0;
                return false;
            }

            result = node.Value;
            isLiteral = node.IsLiteral;

            if (angleMode && !isLiteral && result != 0 && Math.Abs(result) < 1)
                result *= 360;

            return double.IsFinite(result);
        }

        private readonly record struct Node(double Value, bool IsLiteral, bool IsNegated = false);

        private class Parser
        {
            private readonly string text;
            private int position;

            public Parser(string text)
            {
                this.text = text;
            }

            public bool TryParse(out Node node)
            {
                try
                {
                    node = parseAdditive();
                    skipWhitespace();
                    return position == text.Length;
                }
                // invalid syntax, unknown functions or invalid function arguments (e.g. Round(1, 99) or Sign(NaN)).
                catch (Exception e) when (e is FormatException or ArgumentException or ArithmeticException)
                {
                    node = default;
                    return false;
                }
            }

            private Node parseAdditive()
            {
                var left = parseMultiplicative();

                while (true)
                {
                    if (tryConsume('+'))
                        left = new Node(left.Value + parseMultiplicative().Value, false);
                    else if (tryConsume('-'))
                        left = new Node(left.Value - parseMultiplicative().Value, false);
                    else
                        return left;
                }
            }

            private Node parseMultiplicative()
            {
                var left = parseUnary();

                while (true)
                {
                    if (tryConsume('*'))
                        left = new Node(left.Value * parseUnary().Value, false);
                    else if (tryConsume('/'))
                        left = new Node(left.Value / parseUnary().Value, false);
                    else if (tryConsume('%'))
                        left = new Node(left.Value % parseUnary().Value, false);
                    else
                        return left;
                }
            }

            private Node parseUnary()
            {
                if (tryConsume('-'))
                {
                    var operand = parseUnary();
                    // like osu!stable, a negated number still counts as a literal, but anything more complex doesn't.
                    return new Node(-operand.Value, operand.IsLiteral && !operand.IsNegated, true);
                }

                if (tryConsume('+'))
                    return new Node(parseUnary().Value, false);

                return parsePrimary();
            }

            private Node parsePrimary()
            {
                skipWhitespace();

                if (position >= text.Length)
                    throw new FormatException();

                char c = text[position];

                if (c == '(')
                {
                    position++;
                    var inner = parseAdditive();
                    expect(')');
                    return inner;
                }

                if (char.IsDigit(c) || c == '.')
                    return new Node(parseNumber(), true);

                if (char.IsLetter(c))
                    return new Node(parseFunction(), false);

                throw new FormatException();
            }

            private double parseNumber()
            {
                int start = position;

                while (position < text.Length && (char.IsDigit(text[position]) || text[position] == '.'))
                    position++;

                if (position < text.Length && (text[position] == 'e' || text[position] == 'E'))
                {
                    int exponentStart = position++;

                    if (position < text.Length && (text[position] == '+' || text[position] == '-'))
                        position++;

                    if (position < text.Length && char.IsDigit(text[position]))
                    {
                        while (position < text.Length && char.IsDigit(text[position]))
                            position++;
                    }
                    else
                        position = exponentStart;
                }

                if (!double.TryParse(text.AsSpan(start, position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw new FormatException();

                return value;
            }

            private double parseFunction()
            {
                int start = position;

                while (position < text.Length && char.IsLetterOrDigit(text[position]))
                    position++;

                string name = text[start..position].ToLowerInvariant();

                expect('(');

                var args = new List<double>();

                skipWhitespace();

                if (!tryConsume(')'))
                {
                    do
                        args.Add(parseAdditive().Value);
                    while (tryConsume(','));

                    expect(')');
                }

                return callFunction(name, args);
            }

            private static double callFunction(string name, List<double> args)
            {
                switch (name, args.Count)
                {
                    case ("abs", 1): return Math.Abs(args[0]);

                    case ("acos", 1): return Math.Acos(args[0]);

                    case ("asin", 1): return Math.Asin(args[0]);

                    case ("atan", 1): return Math.Atan(args[0]);

                    case ("atan2", 2): return Math.Atan2(args[0], args[1]);

                    case ("ceiling", 1): return Math.Ceiling(args[0]);

                    case ("cos", 1): return Math.Cos(args[0]);

                    case ("exp", 1): return Math.Exp(args[0]);

                    case ("floor", 1): return Math.Floor(args[0]);

                    case ("ieeeremainder", 2): return Math.IEEERemainder(args[0], args[1]);

                    case ("ln", 1): return Math.Log(args[0]);

                    case ("log", 2): return Math.Log(args[0], args[1]);

                    case ("log10", 1): return Math.Log10(args[0]);

                    case ("max", 2): return Math.Max(args[0], args[1]);

                    case ("min", 2): return Math.Min(args[0], args[1]);

                    case ("pow", 2): return Math.Pow(args[0], args[1]);

                    case ("round", 1): return Math.Round(args[0]);

                    case ("round", 2): return Math.Round(args[0], (int)args[1]);

                    case ("sign", 1): return Math.Sign(args[0]);

                    case ("sin", 1): return Math.Sin(args[0]);

                    case ("sqrt", 1): return Math.Sqrt(args[0]);

                    case ("tan", 1): return Math.Tan(args[0]);

                    case ("truncate", 1): return Math.Truncate(args[0]);

                    default:
                        throw new FormatException();
                }
            }

            private void expect(char c)
            {
                if (!tryConsume(c))
                    throw new FormatException();
            }

            private bool tryConsume(char c)
            {
                skipWhitespace();

                if (position < text.Length && text[position] == c)
                {
                    position++;
                    return true;
                }

                return false;
            }

            private void skipWhitespace()
            {
                while (position < text.Length && char.IsWhiteSpace(text[position]))
                    position++;
            }
        }
    }
}
