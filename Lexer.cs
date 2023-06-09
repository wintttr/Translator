﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows.Controls.Ribbon.Primitives;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Translator
{
    using Table = Dictionary<string, int>;
    using Token = ValueTuple<string, int>;
 
    class LexerException : Exception
    {
        public LexerException(string word, int pos)
        : base($"Last word: {word}, Last position: {pos}")
        {}
    }

    class Lexer
    {   
        enum LexerState
        {
            START, IDENTIFIER, INT_CONST,
            FIXED_CONST, FLOATING_SIGN, FLOATING_SECTION,
            STRING_CONST1,STRING_CONST2, OPERATOR,
            COMMENT, ERROR
        };


        private readonly HashSet<string> _keywords = new()
        {
            "if", "else", "while", "function", "return"
        };

        private readonly HashSet<string> _specialIdentifiers = new()
        {
            "TRUE", "FALSE"
        };
         
        private readonly HashSet<string> _operators = new()
        {
            // Арифметические операторы
            "+", "-", "*", "/", "^",

            // Операторы сравнения
            "<", ">", "==", "!=",
            "<=", ">=", 
            
            // Булевы операторы
            "&", "|", 
            
            // Присваивание
            "=", "<-", 
            
            // Другое
            "%%", "!", ":", 
        };

        private readonly HashSet<string> _separators = new()
        {
            "{", "}", "(",
            ")", "[", "]", 
            ",", ";", "\"", "\'"
        };

        public Table KeyWordTable { get; private set; }        // W
        public Table OpTable { get; private set; }             // O
        public Table SepTable { get; private set; }            // R

        public Table IdentifierTable { get; private set; }     // I
        public Table ConstNumTable { get; private set; }       // N
        public Table ConstCharTable { get; private set; }      // C

        private string _currentWord = string.Empty;

        public List<Token> TokensList { get; private set; } = new();

        private bool IsOp(char c)
        {
            foreach (var op in _operators)
                if (op.Contains(c))
                    return true;

            return false;
        }

        private bool IsSep(char c)
        {
            return _separators.Contains(c.ToString());
        }

        private Table JoinWithIndex(IEnumerable<string> hs)
        {
            int index = 0;
            return hs.ToDictionary(x => x, x => index++);
        }

        public Lexer()
        {
            KeyWordTable = JoinWithIndex(_keywords);
            OpTable = JoinWithIndex(_operators);
            SepTable = JoinWithIndex(_separators);

            IdentifierTable = JoinWithIndex(_specialIdentifiers);
            ConstCharTable = new();
            ConstNumTable = new();
        }

        private Token IDSemantic(string s) // Ключевые слова и идентификаторы
        {
            if (KeyWordTable.ContainsKey(s))
                return ("W", KeyWordTable[s]);

            else if (IdentifierTable.ContainsKey(s))
                return ("I", IdentifierTable[s]);

            else
            {
                IdentifierTable.Add(s, IdentifierTable.Count);
                return ("I", IdentifierTable[s]);
            }

        }
        private Token ConstNumSemantic(string s) // Числовые константы
        {
            if (ConstNumTable.ContainsKey(s))
                return ("N", ConstNumTable[s]);
            else
            {
                ConstNumTable.Add(s, ConstNumTable.Count);
                return ("N", ConstNumTable[s]);
            }
        }

        private Token ConstCharSemantic(string s) // Символьные константы
        {
            if (ConstCharTable.ContainsKey(s))
                return ("C", ConstCharTable[s]);
            else
            {
                ConstCharTable.Add(s, ConstCharTable.Count);
                return ("C", ConstCharTable[s]);
            }
        }

        private Token OPSemantic(string s) // Операторы
        {
            return ("O", OpTable[s]);
        }

        private Token SepSemantic(string s) // Разделители
        {
            return ("R", SepTable[s]);
        }

        private LexerState Step(char c, LexerState state)
        {
            RESTEP: 
            switch (state)
            {
                case LexerState.START:
                    _currentWord = c.ToString();

                    if (char.IsLetter(c))
                        state = LexerState.IDENTIFIER;
                    else if (char.IsDigit(c))
                        state = LexerState.INT_CONST;
                    else if (IsOp(c))
                        state = LexerState.OPERATOR;
                    else if (c == '.')
                        state = LexerState.FIXED_CONST;
                    else if (c == '\'')
                        state = LexerState.STRING_CONST1;
                    else if (c == '\"')
                        state = LexerState.STRING_CONST2;
                    else if (c == '#')
                        state = LexerState.COMMENT;
                    else if (char.IsWhiteSpace(c))
                    {
                        // Пропускаем пробелы, табуляции, прочую ересь
                        state = LexerState.START;
                    }
                    else if (IsSep(c))
                    {
                        state = LexerState.START;

                        try
                        {
                            // Обрабатываем разделители на месте
                            TokensList.Add(SepSemantic(_currentWord));
                        }
                        catch (KeyNotFoundException)
                        {
                            return LexerState.ERROR;
                        }
                    }
                    else
                        state = LexerState.ERROR;
                    break;

                case LexerState.IDENTIFIER:
                    if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
                        _currentWord += c;
                    else if (IsOp(c) || IsSep(c) || char.IsWhiteSpace(c))
                    {
                        TokensList.Add(IDSemantic(_currentWord));

                        state = LexerState.START;

                        // Запускаем шаг заново
                        goto RESTEP;
                    }
                    else
                        state = LexerState.ERROR;
                    break;

                case LexerState.INT_CONST:
                    if (char.IsDigit(c))
                    {
                        _currentWord += c;
                    }
                    else if (Char.ToLower(c) == 'e')
                    {
                        _currentWord += c;
                        state = LexerState.FLOATING_SIGN;
                    }
                    else if (c == '.')
                    {
                        _currentWord += c;
                        state = LexerState.FIXED_CONST;
                    }
                    else if (IsOp(c) || IsSep(c) || char.IsWhiteSpace(c))
                    {
                        TokensList.Add(ConstNumSemantic(_currentWord));

                        state = LexerState.START;

                        // Запускаем шаг заново
                        goto RESTEP;
                    }
                    else
                        state = LexerState.ERROR;
                    break;

                case LexerState.FIXED_CONST:
                    if (char.IsDigit(c))
                    {
                        _currentWord += c;
                    }
                    else if(Char.ToLower(c) == 'e')
                    {
                        _currentWord += c;
                        state = LexerState.FLOATING_SIGN;
                    }
                    else if (IsOp(c) || IsSep(c) || char.IsWhiteSpace(c))
                    {
                        TokensList.Add(ConstNumSemantic(_currentWord));

                        state = LexerState.START;

                        // Запускаем шаг заново
                        goto RESTEP;
                    }
                    else
                        state = LexerState.ERROR;
                    break;
                case LexerState.FLOATING_SIGN:
                    state = LexerState.FLOATING_SECTION;

                    if (c == '+' || c == '-')
                    {
                        _currentWord += c;
                    }
                    else
                        // Запускаем шаг заново из состояния FLOATING_SECTION
                        goto RESTEP;
                    
                    break;
                case LexerState.FLOATING_SECTION:
                    if (char.IsDigit(c))
                    {
                        _currentWord += c;
                    }
                    else if (IsOp(c) || IsSep(c) || char.IsWhiteSpace(c))
                    {
                        TokensList.Add(ConstNumSemantic(_currentWord));

                        state = LexerState.START;

                        // Запускаем шаг заново
                        goto RESTEP;
                    }
                    else
                        state = LexerState.ERROR;
                    break;
                case LexerState.COMMENT:
                    if (c == '\n')
                        state = LexerState.START;
                    break;

                case LexerState.STRING_CONST1:
                    _currentWord += c;
                    if (c == '\'')
                    {
                        TokensList.Add(ConstCharSemantic(_currentWord));
                        state = LexerState.START;
                    }
                    break;

                case LexerState.STRING_CONST2:
                    _currentWord += c;
                    if (c == '\"')
                    {
                        TokensList.Add(ConstCharSemantic(_currentWord));
                        state = LexerState.START;
                    }

                    break;

                case LexerState.OPERATOR:
                    if (IsOp(c))
                        _currentWord += c;
                    else if (IsSep(c) || char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                    {
                        try
                        {
                            TokensList.Add(OPSemantic(_currentWord));
                        }
                        catch (KeyNotFoundException)
                        {
                            return LexerState.ERROR;
                        }

                        state = LexerState.START;

                        // Запускаем шаг заново
                        goto RESTEP;
                    }
                    else
                        state = LexerState.START;
                    break;

                case LexerState.ERROR:
                    break;
            }

            return state;
        }

        private void Clear()
        {
            IdentifierTable.Clear();
            ConstNumTable.Clear();
            ConstCharTable.Clear();

            _currentWord = string.Empty;
            TokensList.Clear();
        }

        public List<Token> Run(string s)
        {
            Clear();

            s.Replace('\r', ' ');   // КОСТЫЛЬ 1
            s += "\n";              // КОСТЫЛЬ 2

            LexerState currentState = LexerState.START;

            for (int i = 0; i < s.Length; i++)
            {
                currentState = Step(s[i], currentState);

                if (currentState == LexerState.ERROR)
                    throw new LexerException(_currentWord, i);
            }

            if (currentState != LexerState.START)
                throw new LexerException(_currentWord, s.Length);

            return TokensList;
        }

        public static string TokenListToString(List<Token> tokenList)
        {
            return string.Join(" ", tokenList.Select(x => $"{x.Item1}{x.Item2}"));
        }

        public static string TableToString(Table table)
        {
            return string.Join("\n", table.Select(x => $"{x.Key}: {x.Value}"));
        }
    }
}
