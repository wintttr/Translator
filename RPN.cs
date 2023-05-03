﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Translator
{
    using elbaT = Dictionary<string, int>;
    using Table = List<string>;
    using Token = ValueTuple<string, int>;

    class RPN
    {
        static readonly string _AEM = "AEM";
        static readonly string _FUNC = "FUNC";
        static readonly string _IF = "if";
        static readonly string _WORKED_IF = "~if";
        static readonly string _ELSE = "else";
        static readonly string _UPL = "UPL";
        static readonly string _BP = "BP";
        static readonly string _WHILE = "while";
        static readonly string _WORKED_WHILE = "~while";

        private List<Token> TokenList   { get; init; }
        
        private Table KeyWordTable      { get; init; }           // W
        private Table OpTable           { get; init; }           // O
        private Table SepTable          { get; init; }           // R

        private Table IdentifierTable   { get; init; }           // I
        private Table ConstNumTable     { get; init; }           // N
        private Table ConstCharTable    { get; init; }           // C

        public RPN(List<Token> tokenList, elbaT keyWordTable, elbaT opTable, 
                   elbaT sepTable, elbaT idTable, elbaT numTable, elbaT charTable)
        {
            TokenList = new(tokenList);
            KeyWordTable = ConvertDictionaryToTable(keyWordTable);
            OpTable = ConvertDictionaryToTable(opTable);
            SepTable = ConvertDictionaryToTable(sepTable);
            IdentifierTable = ConvertDictionaryToTable(idTable);
            ConstNumTable = ConvertDictionaryToTable(numTable);
            ConstCharTable = ConvertDictionaryToTable(charTable);
        }

        private static Table ConvertDictionaryToTable(elbaT elbat)
        {
            Table t = new(from el in elbat orderby el.Value select el.Key);
            return t;
        }

        private Table LetterToTable(string l)
        {
            return l switch
            {
                "W" => KeyWordTable,
                "O" => OpTable,
                "R" => SepTable,
                "I" => IdentifierTable,
                "N" => ConstNumTable,
                "C" => ConstCharTable,
                _ => throw new Exception("INTERNAL ERROR AAAAAAAA")
            };
        }

        static private readonly Dictionary<string, int> _operationPriority = new()
        {
            { "(",    0 },
            { "[",    0 },
            { "{",    0 },
            { _AEM,   0 },
            { _FUNC,  0 },
            { _IF,    0 },
            { _WHILE, 0 },
            { _WORKED_IF, 0 },

            { ",",    1 },
            { ";",    1 },
            { ")",    1 },
            { "}",    1 },
            { "]",    1 },
            { _ELSE,  1 },

            { "=",    2 },
            { "<-",   2 },

            { "|",    3 },
            { "&",    4 },
            { "!",    5 },

            { ">",    6 },
            { "<",    6 },
            { "<=",   6 },
            { ">=",   6 },
            { "==",   6 },
            { "!=",   6 },

            { "+",    7 },
            { "-",    7 },

            { "*",    8 },
            { "/",    8 },

            { "^",    9 },
        };

        private string GetStringByToken(Token t)
        {
            return LetterToTable(t.Item1)[t.Item2];
        }

        private static int GetOperationPriority(string op)
        {
            return _operationPriority[op];
        }

        private bool IsOperand(Token t)
        {
            return t.Item1 == "I" || t.Item1 == "N" || t.Item1 == "C";
        }

        static private void AddToStringBuilder(StringBuilder sb, string s)
        {
            sb.Append(s);
            sb.Append(' ');
        }

        static private bool IsIdentifier(Token t)
        {
            return t.Item1 == "I";
        }

        private bool IsIf(Token t)
        {
            return GetStringByToken(t) == "if";
        }

        private bool IsWhile(Token t)
        {
            return GetStringByToken(t) == "while";
        }

        public string GetRPN()
        {
            Stack<string> _stack = new();
            StringBuilder sb = new();
            int WMarkCount = 1;
            int IfMarkCount = 1;
            Token PrevToken = TokenList[0];

            foreach(Token t in TokenList)
            {
                if (IsOperand(t))
                    AddToStringBuilder(sb, GetStringByToken(t));
                else
                {
                    string currentOperation = GetStringByToken(t);
                    int currentPriority = GetOperationPriority(currentOperation);

                    if (!_operationPriority.ContainsKey(currentOperation))
                        throw new Exception($"Unknown operation: {currentOperation}");

                    if (currentOperation == "(")
                    {
                        if (IsIdentifier(PrevToken))
                        {
                            _stack.Push("2");
                            _stack.Push(_FUNC);
                        }
                        else if (IsIf(PrevToken))
                        {
                            // NOTHING
                        }
                        else if (IsWhile(PrevToken))
                        {
                            // NOTHING
                        }
                        else
                            _stack.Push(currentOperation);
                    }
                    else if (currentOperation == ")")
                    {
                        while (_stack.Peek() != "(" && _stack.Peek() != _FUNC &&
                               _stack.Peek() != _IF && _stack.Peek() != _WORKED_IF &&
                               _stack.Peek() != _WHILE)
                            AddToStringBuilder(sb, _stack.Pop());

                        string instruction = _stack.Pop();
                        if (instruction == _FUNC)
                        {
                            int FValue = Int32.Parse(_stack.Pop());
                            AddToStringBuilder(sb, $"{FValue}");
                            AddToStringBuilder(sb, _FUNC);
                        }
                        else if (instruction == _IF)
                        {
                            _stack.Push(_WORKED_IF);

                            AddToStringBuilder(sb, $"M{IfMarkCount}");
                            AddToStringBuilder(sb, _UPL);
                        }
                        else if (instruction == _WHILE)
                        {
                            _stack.Push(_WORKED_WHILE);

                            WMarkCount++;
                            AddToStringBuilder(sb, $"W{WMarkCount}");
                            AddToStringBuilder(sb, _UPL);
                        }
                        else if (instruction == _WORKED_IF)
                        {
                            while (_stack.Peek() != "(")
                                AddToStringBuilder(sb, _stack.Pop());
                            _stack.Pop();

                            AddToStringBuilder(sb, $"M{IfMarkCount}:");
                        }
                    }
                    else if(currentOperation == "{")
                    {
                        _stack.Push(currentOperation);
                    }
                    else if(currentOperation == "}")
                    {
                        while (_stack.Peek() != "{")
                        {
                            string instruction = _stack.Pop();
                            if(instruction != _WORKED_IF && instruction != _WORKED_WHILE)
                                AddToStringBuilder(sb, instruction);
                            
                        }
                        _stack.Pop();
                    }
                    else if (currentOperation == _IF)
                    {
                        _stack.Push(_IF);
                    }
                    else if (currentOperation == _WHILE)
                    {
                        AddToStringBuilder(sb, $"W{WMarkCount}:");
                        _stack.Push(_WHILE);
                    }
                    else if(currentOperation == _ELSE)
                    {
                        while (_stack.Peek() != _WORKED_IF)
                            AddToStringBuilder(sb, _stack.Pop());

                        IfMarkCount++;
                        AddToStringBuilder(sb, $"M{IfMarkCount}");
                        AddToStringBuilder(sb, _BP);
                        AddToStringBuilder(sb, $"M{IfMarkCount-1}:");
                    }
                    else if(currentOperation == ";")
                    {
                        // DO NOTHING
                    }
                    else if(currentOperation == "[")
                    {
                        if (IsIdentifier(PrevToken))
                        {
                            _stack.Push("2");
                            _stack.Push(_AEM);
                        }
                        else
                            throw new Exception("Syntax error");
                    }
                    else if(currentOperation == ",")
                    {
                        while (_stack.Peek() != _AEM && _stack.Peek() != _FUNC)
                            AddToStringBuilder(sb, _stack.Pop());

                        string instruction = _stack.Pop(); // Pop aem or func

                        int value = Int32.Parse(_stack.Pop()) + 1;
                        _stack.Push(value.ToString());
                        _stack.Push(instruction);
                    }
                    else if(currentOperation == "]")
                    {
                        while (!_stack.Peek().Contains(_AEM))
                            AddToStringBuilder(sb, _stack.Pop());

                        _stack.Pop();

                        int AEMValue = Int32.Parse(_stack.Pop());
                        AddToStringBuilder(sb, $"{AEMValue}");
                        AddToStringBuilder(sb, _AEM);
                    }
                    else if (_stack.Count == 0 || GetOperationPriority(_stack.Peek()) < currentPriority)
                        _stack.Push(currentOperation);
                    else
                    {
                        while (_stack.Count > 0 && GetOperationPriority(_stack.Peek()) >= currentPriority)
                        {
                            string instruction = _stack.Pop();
                            if (instruction == _WORKED_WHILE)
                            {
                                AddToStringBuilder(sb, $"W{WMarkCount - 1}");
                                AddToStringBuilder(sb, $"{_BP}");
                                AddToStringBuilder(sb, $"W{WMarkCount}:");
                            }
                            else if (instruction == _WORKED_IF)
                                AddToStringBuilder(sb, $"M{IfMarkCount}:");
                            else
                                AddToStringBuilder(sb, instruction);
                        }

                        _stack.Push(currentOperation);
                    }
                }

                PrevToken = t;
            }

            while(_stack.Count > 0)
            {
                string instruction = _stack.Pop();
                if (instruction == _WORKED_WHILE)
                {
                    AddToStringBuilder(sb, $"W{WMarkCount - 1}");
                    AddToStringBuilder(sb, $"{_BP}");
                    AddToStringBuilder(sb, $"W{WMarkCount}:");
                }
                else if (instruction == _WORKED_IF)
                    AddToStringBuilder(sb, $"M{IfMarkCount}:");
                else
                    AddToStringBuilder(sb, instruction);
            }

            return sb.ToString();
        }

    }
}
