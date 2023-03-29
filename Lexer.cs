using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows.Media;

namespace Translator
{
    using Table = Dictionary<string, int>;
 
    class LexerException : Exception
    {
        public LexerException(string word, int pos)
        : base(String.Format("Last word: {0}, Last position: {1}", word, pos))
        {}
    }

    class Lexer
    {   
        enum LexerState
        {
            START, IDENTIFIER, INT_CONST,
            FLOAT_CONST, STRING_CONST1,
            STRING_CONST2, OPERATOR,
            COMMENT, ERROR
        };


        private readonly HashSet<string> _keywords = new()
        {
            "if", "else", "while"
        };

        private readonly HashSet<string> _operators = new()
        {
            "+", "-", "*", "/",
            "<", ">", "==", "!=",
            "<=", ">=", "&", "|", "=",
            "<-", "%%", "!", ":"
        };
        private readonly HashSet<string> _separators = new()
        {
            "{", "}", "(",
            ")", "[", "]", 
            ",", ";", "."
        };

        public Table KeyWordTable { get; private set; }        // W
        public Table OpTable { get; private set; }             // O
        public Table SepTable { get; private set; }            // R

        public Table IdentifierTable { get; private set; }     // I
        public Table ConstNumTable { get; private set; }       // N
        public Table ConstCharTable { get; private set; }      // C

        private string _currentWord = string.Empty;
        private bool _stepBack = false;

        private bool IsStepBack()
        {
            if (_stepBack == true)
            {
                _stepBack = false;
                return true;
            }
            else 
                return false;
        }

        private void SetStepBack()
        {
            _stepBack = true;
        }

        private List<(string, int)> _tokensList = new();

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

        private Table JoinWithIndex(HashSet<string> hs)
        {
            int index = 0;
            return hs.ToDictionary(x => x, x => index++);
        }

        public Lexer()
        {
            KeyWordTable = JoinWithIndex(_keywords);
            OpTable = JoinWithIndex(_operators);
            SepTable = JoinWithIndex(_separators);

            IdentifierTable = new();
            ConstCharTable = new();
            ConstNumTable = new();
        }

        private (string, int) IDSemantic(string s) // Ключевые слова и идентификаторы
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
        private (string, int) ConstNumSemantic(string s) // Числовые константы
        {
            if (ConstNumTable.ContainsKey(s))
                return ("N", ConstNumTable[s]);
            else
            {
                ConstNumTable.Add(s, ConstNumTable.Count);
                return ("N", ConstNumTable[s]);
            }
        }

        private (string, int) ConstCharSemantic(string s) // Символьные константы
        {
            if (ConstCharTable.ContainsKey(s))
                return ("C", ConstCharTable[s]);
            else
            {
                ConstCharTable.Add(s, ConstCharTable.Count);
                return ("C", ConstCharTable[s]);
            }
        }

        private (string, int) OPSemantic(string s) // Операторы
        {
            return ("O", OpTable[s]);
        }

        private (string, int) SepSemantic(string s) // Разделители
        {
            return ("R", SepTable[s]);
        }

        private LexerState Step(char c, LexerState state)
        {
            LexerState newState = state;

            switch (state)
            {
                case LexerState.START:
                    _currentWord = c.ToString();

                    if (char.IsLetter(c))
                        newState = LexerState.IDENTIFIER;
                    else if (char.IsDigit(c))
                        newState = LexerState.INT_CONST;
                    else if (IsOp(c))
                        newState = LexerState.OPERATOR;
                    else if (IsSep(c)) 
                    {
                        // Обрабатываем разделители на месте
                        _tokensList.Add(SepSemantic(_currentWord));
                    }
                    else if (c == '.')
                        newState = LexerState.FLOAT_CONST;
                    else if (c == '\'')
                        newState = LexerState.STRING_CONST1;
                    else if (c == '\"')
                        newState = LexerState.STRING_CONST2;
                    else if (c == '#')
                        newState = LexerState.COMMENT;
                    else if (char.IsWhiteSpace(c)) 
                    {
                        // Пропускаем пробелы, табуляции, прочую ересь
                    }
                    else
                        newState = LexerState.ERROR;
                    break;

                case LexerState.IDENTIFIER:
                    if (char.IsLetterOrDigit(c) || c == '_')
                        _currentWord += c;
                    else if (IsOp(c) || IsSep(c))
                    {
                        SetStepBack();
                        _tokensList.Add(IDSemantic(_currentWord));
                        newState = LexerState.START;
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        _tokensList.Add(IDSemantic(_currentWord));
                        newState = LexerState.START;
                    }
                    else
                        newState = LexerState.ERROR;
                    break;

                case LexerState.INT_CONST:
                    if (char.IsDigit(c))
                    {
                        _currentWord += c;
                    }
                    else if (IsOp(c) || IsSep(c))
                    {
                        SetStepBack();
                        _tokensList.Add(ConstNumSemantic(_currentWord));
                        newState = LexerState.START;
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        _tokensList.Add(ConstNumSemantic(_currentWord));
                        newState = LexerState.START;
                    }
                    else if (c == '.')
                    {
                        _currentWord += c;
                        newState = LexerState.FLOAT_CONST;
                    }
                    else
                        newState = LexerState.ERROR;
                    break;

                case LexerState.FLOAT_CONST:
                    if (char.IsDigit(c))
                    {
                        _currentWord += c;
                    }
                    else if (IsOp(c) || IsSep(c))
                    {
                        SetStepBack();
                        _tokensList.Add(ConstNumSemantic(_currentWord));
                        newState = LexerState.START;
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        _tokensList.Add(ConstNumSemantic(_currentWord));
                        newState = LexerState.START;
                    }
                    else
                        newState = LexerState.ERROR;
                    break;

                case LexerState.COMMENT:
                    if (c == '\n')
                        newState = LexerState.START;
                    break;

                case LexerState.STRING_CONST1:
                    _currentWord += c;
                    if (c == '\'')
                    {
                        _tokensList.Add(ConstCharSemantic(_currentWord));
                        newState = LexerState.START;
                    }
                    break;

                case LexerState.STRING_CONST2:
                    _currentWord += c;
                    if (c == '\"')
                    {
                        _tokensList.Add(ConstCharSemantic(_currentWord));
                        newState = LexerState.START;
                    }

                    break;

                case LexerState.OPERATOR:
                    if (IsOp(c))
                        _currentWord += c;
                    else if (IsSep(c) || char.IsLetterOrDigit(c))
                    {
                        SetStepBack();
                        try
                        {
                            _tokensList.Add(OPSemantic(_currentWord));
                        }
                        catch (KeyNotFoundException)
                        {
                            return LexerState.ERROR;
                        }

                        newState = LexerState.START;
                    }
                    else if (char.IsWhiteSpace(c)) 
                    {
                        try
                        {
                            _tokensList.Add(OPSemantic(_currentWord));
                        }
                        catch (KeyNotFoundException)
                        {
                            return LexerState.ERROR;
                        }

                        newState = LexerState.START;
                    }
                    else
                        newState = LexerState.ERROR;
                    break;

                case LexerState.ERROR:
                    break;
            }

            return newState;
        }

        private void Clear()
        {
            IdentifierTable.Clear();
            ConstNumTable.Clear();
            ConstCharTable.Clear();

            _currentWord = string.Empty;
            _tokensList.Clear();
        }

        public List<(string, int)> Run(string s)
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

                if (IsStepBack())
                    i--;
            }

            return _tokensList;
        }

        public static string TokenListToString(List<(string, int)> tokenList)
        {
            return string.Join(" ", tokenList.Select(x => String.Format("({0}, {1})", x.Item1, x.Item2)));
        }

        public static string TableToString(Table table)
        {
            return string.Join("\n", table.Select(x => String.Format("{0}: {1}", x.Key, x.Value)));
        }
    }
}
