using System;
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

    class StringIntStack
    {
        private Stack<string> _stack = new();

        public void Push(string s) => _stack.Push(s);

        public void Push(int i) => _stack.Push(i.ToString());

        public string Pop() => _stack.Pop();

        public int PopInt() => Int32.Parse(_stack.Pop());
        

        public string Peek() => _stack.Peek();

        public bool Empty => _stack.Count == 0;
    }
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
            return t.Item1 == "I" || t.Item1 == "N" || t.Item1 == "C" || GetStringByToken(t) == "function";
        }

        static private void AddToStringBuilder(StringBuilder sb, string s)
        {
            sb.Append(s);
            sb.Append(' ');
        }

        private bool IsIdentifier(Token t)
        {
            return t.Item1 == "I" || GetStringByToken(t) == "function";
        }

        private bool IsIf(Token t)
        {
            return GetStringByToken(t) == "if";
        }

        private bool IsWhile(Token t)
        {
            return GetStringByToken(t) == "while";
        }

        static private void ProcessIfAndWhile(StringBuilder sb, string instruction, int WMarkCount, int IfMarkCount)
        {
            if (instruction == _WORKED_WHILE)
            {
                AddToStringBuilder(sb, $"W{WMarkCount - 1}");
                AddToStringBuilder(sb, $"{_BP}");
                AddToStringBuilder(sb, $"W{WMarkCount}:");
            }
            else if (instruction == _WORKED_IF)
                AddToStringBuilder(sb, $"M{IfMarkCount + 1}:");
            else
                AddToStringBuilder(sb, instruction);
        }

        public string GetRPN()
        {
            StringIntStack stack = new();
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
                            stack.Push(2);
                            stack.Push(_FUNC);
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
                            stack.Push(currentOperation);
                    }
                    else if (currentOperation == ")")
                    {
                        while (stack.Peek() != "(" && stack.Peek() != _FUNC &&
                               stack.Peek() != _IF && stack.Peek() != _WORKED_IF &&
                               stack.Peek() != _WHILE)
                            AddToStringBuilder(sb, stack.Pop());

                        string instruction = stack.Pop();
                        if (instruction == _FUNC)
                        {
                            int FValue = stack.PopInt();
                            AddToStringBuilder(sb, $"{FValue}");
                            AddToStringBuilder(sb, _FUNC);
                        }
                        else if (instruction == _IF)
                        {
                            stack.Push(_WORKED_IF);

                            AddToStringBuilder(sb, $"M{IfMarkCount}");
                            AddToStringBuilder(sb, _UPL);
                        }
                        else if (instruction == _WHILE)
                        {
                            stack.Push(_WORKED_WHILE);

                            WMarkCount++;
                            AddToStringBuilder(sb, $"W{WMarkCount}");
                            AddToStringBuilder(sb, _UPL);
                        }
                        else if (instruction == _WORKED_IF)
                        {
                            while (stack.Peek() != "(")
                                AddToStringBuilder(sb, stack.Pop());
                            stack.Pop();

                            AddToStringBuilder(sb, $"M{IfMarkCount}:");
                        }
                    }
                    else if(currentOperation == "{")
                    {
                        IfMarkCount += 2;
                        WMarkCount += 2;

                        stack.Push(currentOperation);
                    }
                    else if(currentOperation == "}")
                    {
                        while (stack.Peek() != "{")
                        {
                            string instruction = stack.Pop();
                            ProcessIfAndWhile(sb, instruction, WMarkCount, IfMarkCount);

                        }
                        stack.Pop();

                        IfMarkCount -= 2;
                        WMarkCount -= 2;
                    }
                    else if (currentOperation == _IF)
                    {
                        stack.Push(_IF);
                    }
                    else if (currentOperation == _WHILE)
                    {
                        AddToStringBuilder(sb, $"W{WMarkCount}:");
                        stack.Push(_WHILE);
                    }
                    else if(currentOperation == _ELSE)
                    {
                        while (stack.Peek() != _WORKED_IF)
                            AddToStringBuilder(sb, stack.Pop());

                        AddToStringBuilder(sb, $"M{IfMarkCount + 1}");
                        AddToStringBuilder(sb, _BP);
                        AddToStringBuilder(sb, $"M{IfMarkCount}:");
                    }
                    else if(currentOperation == ";")
                    {
                        // DO NOTHING
                    }
                    else if(currentOperation == "[")
                    {
                        if (IsIdentifier(PrevToken))
                        {
                            stack.Push(2);
                            stack.Push(_AEM);
                        }
                        else
                            throw new Exception("Syntax error");
                    }
                    else if(currentOperation == ",")
                    {
                        while (stack.Peek() != _AEM && stack.Peek() != _FUNC)
                            AddToStringBuilder(sb, stack.Pop());

                        string instruction = stack.Pop(); // Pop aem or func

                        int value = stack.PopInt() + 1;
                        stack.Push(value);
                        stack.Push(instruction);
                    }
                    else if(currentOperation == "]")
                    {
                        while (stack.Peek() != _AEM)
                            AddToStringBuilder(sb, stack.Pop());

                        stack.Pop();

                        int AEMValue = stack.PopInt();
                        AddToStringBuilder(sb, $"{AEMValue}");
                        AddToStringBuilder(sb, _AEM);
                    }
                    else if (stack.Empty || GetOperationPriority(stack.Peek()) < currentPriority)
                        stack.Push(currentOperation);
                    else
                    {
                        while (!stack.Empty && GetOperationPriority(stack.Peek()) >= currentPriority)
                        {
                            string instruction = stack.Pop();
                            ProcessIfAndWhile(sb, instruction, WMarkCount, IfMarkCount);
                        }

                        stack.Push(currentOperation);
                    }
                }

                PrevToken = t;
            }

            while(!stack.Empty)
            {
                string instruction = stack.Pop();
                ProcessIfAndWhile(sb, instruction, WMarkCount, IfMarkCount);
            }

            return sb.ToString();
        }

    }
}
