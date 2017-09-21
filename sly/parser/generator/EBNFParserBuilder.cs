using sly.lexer;

using sly.parser.generator;
using System.Reflection;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Principal;
using System.Xml;
using sly.parser.syntax;
using sly.parser.llparser;


namespace sly.parser.generator
{


    /// <summary>
    /// this class provides API to build parser
    /// </summary>
    internal class EBNFParserBuilder<IN,OUT> : ParserBuilder<IN,OUT> where IN :struct
    {

        public EBNFParserBuilder()
        {

        }


        public override Parser<IN,OUT> BuildParser(object parserInstance, ParserType parserType, string rootRule)
        {
            
            RuleParser<IN> ruleparser = new RuleParser<IN>();
            ParserBuilder<EbnfToken, GrammarNode<IN>> builder = new ParserBuilder<EbnfToken, GrammarNode<IN>>();

            Parser<EbnfToken,GrammarNode<IN>> grammarParser = builder.BuildParser(ruleparser, ParserType.LL_RECURSIVE_DESCENT, "rule");

            //ParserBuilder builder = new ParserBuilder();
//            EBNFParserBuilder<EbnfToken,GrammarNode<IN>> parserGrammar = new EBNFParserBuilder<EbnfToken,GrammarNode<IN>>();
//            if (GrammarParser == null)
//            {
//                GrammarParser = builder.BuildParser<EbnfToken,GrammarNode<IN>>(parserGrammar, ParserType.LL_RECURSIVE_DESCENT, "rule");
//            }
            ParserConfiguration<IN,OUT> configuration =
                ExtractEbnfParserConfiguration(parserInstance.GetType(), grammarParser);

            ISyntaxParser<IN> syntaxParser = BuildSyntaxParser(configuration, parserType, rootRule);

            SyntaxTreeVisitor<IN, OUT> visitor = null;
            if (parserType == ParserType.LL_RECURSIVE_DESCENT)
            {
                new SyntaxTreeVisitor<IN, OUT>(configuration, parserInstance);
            }
            else if (parserType == ParserType.EBNF_LL_RECURSIVE_DESCENT)
            {
                visitor = new EBNFSyntaxTreeVisitor<IN,OUT>(configuration, parserInstance);
            }
            Parser<IN,OUT> parser = new Parser<IN,OUT>(syntaxParser, visitor);
            parser.Configuration = configuration;
            parser.Lexer = BuildLexer<IN>();
            parser.Instance = parserInstance;
            return parser;
        }

        [LexerConfiguration]
        public ILexer<EbnfToken> BuildEbnfLexer(ILexer<EbnfToken> lexer)
        {
            lexer.AddDefinition(new TokenDefinition<EbnfToken>(EbnfToken.COLON, ":"));
            lexer.AddDefinition(new TokenDefinition<EbnfToken>(EbnfToken.ONEORMORE, "\\+"));
            lexer.AddDefinition(new TokenDefinition<EbnfToken>(EbnfToken.ZEROORMORE, "\\*"));
            lexer.AddDefinition(new TokenDefinition<EbnfToken>(EbnfToken.IDENTIFIER,
                "[A-Za-z0-9_��������][A-Za-z0-9_��������]*"));
            lexer.AddDefinition(new TokenDefinition<EbnfToken>(EbnfToken.COLON, ":"));
            lexer.AddDefinition(new TokenDefinition<EbnfToken>(EbnfToken.WS, "[ \\t]+", true));
            lexer.AddDefinition(new TokenDefinition<EbnfToken>(EbnfToken.EOL, "[\\n\\r]+", true, true));
            return lexer;
        }




        protected override ISyntaxParser<IN> BuildSyntaxParser(ParserConfiguration<IN,OUT> conf, ParserType parserType,
            string rootRule)
        {
            ISyntaxParser<IN> parser = null;
            switch (parserType)
            {
                case ParserType.LL_RECURSIVE_DESCENT:
                    {
                        parser = (ISyntaxParser<IN>)(new RecursiveDescentSyntaxParser<IN,OUT>(conf, rootRule));
                        break;
                    }
                case ParserType.EBNF_LL_RECURSIVE_DESCENT:
                {
                    parser = (ISyntaxParser<IN>)(new EBNFRecursiveDescentSyntaxParser<IN,OUT>(conf, rootRule));
                    break;
                }
                default:
                    {
                        parser = null;
                        break;
                    }
            }
            return parser;
        }


        #region configuration

        protected virtual ParserConfiguration<IN,OUT> ExtractEbnfParserConfiguration(Type parserClass,
            Parser<EbnfToken,GrammarNode<IN>> grammarParser)
        {
            ParserConfiguration<IN,OUT> conf = new ParserConfiguration<IN,OUT>();
            Dictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
            Dictionary<string, NonTerminal<IN>> nonTerminals = new Dictionary<string, NonTerminal<IN>>();
            List<MethodInfo> methods = parserClass.GetMethods().ToList<MethodInfo>();
            methods = methods.Where(m =>
            {
                List<Attribute> attributes = m.GetCustomAttributes().ToList<Attribute>();
                Attribute attr = attributes.Find(a => a.GetType() == typeof(ProductionAttribute));
                return attr != null;
            }).ToList<MethodInfo>();

            methods.ForEach(m =>
            {
                ProductionAttribute[] attributes =
                    (ProductionAttribute[])m.GetCustomAttributes(typeof(ProductionAttribute), true);

                foreach (ProductionAttribute attr in attributes)
                {

                    string ruleString = attr.RuleString;
                    ParseResult<EbnfToken,GrammarNode<IN>> parseResult = grammarParser.Parse(ruleString);
                    if (!parseResult.IsError)
                    {
                        Rule<IN> rule = (Rule<IN>)parseResult.Result;
                        functions[rule.NonTerminalName+"__"+rule.Key] = m;


                        NonTerminal<IN> nonT = null;
                        if (!nonTerminals.ContainsKey(rule.NonTerminalName))
                        {
                            nonT = new NonTerminal<IN>(rule.NonTerminalName, new List<Rule<IN>>());
                        }
                        else
                        {
                            nonT = nonTerminals[rule.NonTerminalName];
                        }
                        nonT.Rules.Add(rule);
                        nonTerminals[rule.NonTerminalName] = nonT;
                    }
                    else
                    {
                        string message = parseResult
                            .Errors
                            .Select(e => e.ErrorMessage)
                            .Aggregate<string>((e1, e2) => e1 + "\n" + e2);
                        throw new ParserConfigurationException(message);
                    }
                }



            });



            conf.Functions = functions;
            conf.NonTerminals = nonTerminals;

            return conf;
        }

        #endregion

      

    }


}