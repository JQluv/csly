﻿using cpg.parser.parsgenerator.generator;
using cpg.parser.parsgenerator.syntax;
using lexer;
using parser.parsergenerator.generator;
using parser.parsergenerator.parser;
using parser.parsergenerator.syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpg.parser.parsgenerator.parser.llparser
{

    class ParserState<T>
    {

        int CurrentTokenPosition { get; set; }

        IList<Token<T>> Tokens { get; set; }

        Rule<T> Rule { get; set; }

        public ParserState(int position, Rule<T> rule, IList<Token<T>> tokens)
        {
            this.CurrentTokenPosition = position;
            this.Rule = rule;
            this.Tokens = tokens;
        }


    }

    public class RecursiveDescentSyntaxParser<T> : ISyntaxParser<T>
    {
        public ParserConfiguration<T> Configuration { get; set; }

        public string StartingNonTerminal { get; set; }


        public RecursiveDescentSyntaxParser(ParserConfiguration<T> configuration, string startingNonTerminal)
        {
            Configuration = configuration;
            StartingNonTerminal = startingNonTerminal;
            InitializeStartingTokens<T>(Configuration, startingNonTerminal);
        }

        #region STARTING_TOKENS


        static private void InitializeStartingTokens<T>(ParserConfiguration<T> configuration, string root)
        {


            Dictionary<string, NonTerminal<T>> nts = configuration.NonTerminals;


            InitStartingTokensForNT(nts, root);
            foreach (NonTerminal<T> nt in nts.Values)
            {
                foreach (Rule<T> rule in nt.Rules)
                {
                    if (rule.PossibleLeadingTokens == null || rule.PossibleLeadingTokens.Count == 0)
                    {
                        InitStartingTokensForRule<T>(nts, rule);
                    }
                }
            }
        }

        static private void InitStartingTokensForNT<T>(Dictionary<string, NonTerminal<T>> nonTerminals, string name)
        {
            if (nonTerminals.ContainsKey(name))
            {
                NonTerminal<T> nt = nonTerminals[name];
                nt.Rules.ForEach(r => InitStartingTokensForRule<T>(nonTerminals, r));
            }
            return;
        }

        static private void InitStartingTokensForRule<T>(Dictionary<string, NonTerminal<T>> nonTerminals, Rule<T> rule)
        {
            if (rule.PossibleLeadingTokens == null || rule.PossibleLeadingTokens.Count == 0)
            {
                rule.PossibleLeadingTokens = new List<T>();
                if (rule.Clauses.Count > 0)
                {
                    Clause<T> first = rule.Clauses[0];
                    if (first is TerminalClause<T>)
                    {
                        TerminalClause<T> term = first as TerminalClause<T>;
                        rule.PossibleLeadingTokens.Add(term.ExpectedToken);
                        rule.PossibleLeadingTokens = rule.PossibleLeadingTokens.Distinct<T>().ToList<T>();
                    }
                    else
                    {
                        NonTerminalClause<T> nonterm = first as NonTerminalClause<T>;
                        InitStartingTokensForNT<T>(nonTerminals, nonterm.NonTerminalName);
                        NonTerminal<T> firstNonTerminal = nonTerminals[nonterm.NonTerminalName];
                        firstNonTerminal.Rules.ForEach(r =>
                        {
                            rule.PossibleLeadingTokens.AddRange(r.PossibleLeadingTokens);
                        });
                        rule.PossibleLeadingTokens = rule.PossibleLeadingTokens.Distinct<T>().ToList<T>();
                    }
                }

            }
        }

        #endregion



        public SyntaxParseResult<T> Parse(IList<Token<T>> tokens)
        {
            Dictionary<string, NonTerminal<T>> NonTerminals = Configuration.NonTerminals;
            List<UnexpectedTokenSyntaxError<T>> errors = new List<UnexpectedTokenSyntaxError<T>>();
            NonTerminal<T> nt = NonTerminals[StartingNonTerminal];

            List<Rule<T>> rules = nt.Rules.Where<Rule<T>>(r => r.PossibleLeadingTokens.Contains(tokens[0].TokenID)).ToList<Rule<T>>();

            List<SyntaxParseResult<T>> rs = new List<SyntaxParseResult<T>>();
            foreach (Rule<T> rule in rules)
            {
                SyntaxParseResult<T> r = Parse(tokens, rule, 0);
                if (!r.IsError)
                {
                    rs.Add(r);
                }
            }
            SyntaxParseResult<T> result = null;

            
            if (rs.Count > 0)
            {
                result = rs.Find(r => r.IsEnded && !r.IsError);

                if (result == null)
                {
                    List<int> endingPositions = rs.Select(r => r.EndingPosition).ToList<int>();
                    int lastposition = endingPositions.Max();
                    List<SyntaxParseResult<T>> furtherResults = rs.Where<SyntaxParseResult<T>>(r => r.EndingPosition == lastposition).ToList<SyntaxParseResult<T>>();
                    errors.Add(new UnexpectedTokenSyntaxError<T>(tokens[lastposition], null));                    
                    furtherResults.ForEach(r =>
                    {
                        if (r.Errors != null)
                        {                            
                            errors.AddRange(r.Errors);
                        }
                    });
                }

            }
            if (result == null)
            {
                result = new SyntaxParseResult<T>();
                result.Errors = errors;
                result.IsError = true;
            }
            return result;

        }


        public SyntaxParseResult<T> Parse(IList<Token<T>> tokens, Rule<T> rule, int position)
        {
            int currentPosition = position;
            List<UnexpectedTokenSyntaxError<T>> errors = new List<UnexpectedTokenSyntaxError<T>>();
            bool isError = false;
            List<IConcreteSyntaxNode<T>> children = new List<IConcreteSyntaxNode<T>>();
            if (rule.PossibleLeadingTokens.Contains(tokens[position].TokenID))
            {
                if (rule.Clauses != null && rule.Clauses.Count > 0)
                {
                    children = new List<IConcreteSyntaxNode<T>>();
                    foreach (Clause<T> clause in rule.Clauses)
                    {
                        if (clause is TerminalClause<T>)
                        {
                            SyntaxParseResult<T> termRes = ParseTerminal(tokens, clause as TerminalClause<T>, currentPosition);
                            if (!termRes.IsError)
                            {
                                children.Add(termRes.Root);
                                currentPosition = termRes.EndingPosition;
                            }
                            else
                            {
                                Token<T> tok = tokens[currentPosition];
                                errors.Add(new UnexpectedTokenSyntaxError<T>(tokens[currentPosition], ((TerminalClause<T>)clause).ExpectedToken));
                            }
                            isError = isError || termRes.IsError;
                        }
                        else if (clause is NonTerminalClause<T>)
                        {
                            NonTerminalClause<T> nonTermClause = clause as NonTerminalClause<T>;
                            NonTerminal<T> nt = Configuration.NonTerminals[nonTermClause.NonTerminalName];
                            bool found = false;
                            int i = 0;

                            bool eos = tokens[currentPosition].IsEndOfStream;
                            List<T> allAcceptableTokens = new List<T>();
                            nt.Rules.ForEach(r =>
                            {
                                if (r != null && r.PossibleLeadingTokens != null)
                                {
                                    allAcceptableTokens.AddRange(r.PossibleLeadingTokens);
                                }
                                else
                                {
                                    ;
                                }
                            }); 
                            allAcceptableTokens = allAcceptableTokens.Distinct<T>().ToList<T>();
                                                        
                            List<Rule<T>> rules = nt.Rules.Where<Rule<T>>(r => r.PossibleLeadingTokens.Contains(tokens[currentPosition].TokenID) || r.IsEmpty).ToList<Rule<T>>();

                            if (rules.Count == 0)
                            {
                                isError = true;
                                errors.Add(new UnexpectedTokenSyntaxError<T>(tokens[currentPosition], allAcceptableTokens.ToArray<T>()));
                            }

                            List<UnexpectedTokenSyntaxError<T>> innerRuleErrors = new List<UnexpectedTokenSyntaxError<T>>();
                            SyntaxParseResult<T> okResult = null;
                            int greaterIndex = 0;
                            bool allRulesInError = true;
                            while (!found && i < rules.Count)
                            {
                                Rule<T> innerrule = rules[i];
                                SyntaxParseResult<T> innerRuleRes = Parse(tokens, innerrule, currentPosition);
                                if (!innerRuleRes.IsError && okResult == null)
                                {
                                    okResult = innerRuleRes;
                                    children.Add(innerRuleRes.Root);
                                    found = true;
                                    currentPosition = innerRuleRes.EndingPosition;
                                }

                                if (innerRuleRes.EndingPosition > greaterIndex && innerRuleRes.Errors != null  && innerRuleRes.Errors.Any() )
                                {
                                    greaterIndex = innerRuleRes.EndingPosition;
                                    innerRuleErrors.Clear();
                                    innerRuleErrors.AddRange(innerRuleRes.Errors);
                                }
                                allRulesInError = allRulesInError && innerRuleRes.IsError;
                                //isError = isError && innerRuleRes.IsError; 
                                i++;
                            }
                            isError = isError || allRulesInError;
                            errors.AddRange(innerRuleErrors);
                        }
                        else
                        {
                            ;
                        }
                        if (isError)
                        {                            
                            break;
                        }                        
                    }
                }
            }

            SyntaxParseResult<T> result = new SyntaxParseResult<T>();
            result.IsError = isError;
            result.Errors = errors;
            result.EndingPosition = currentPosition;
            if (!isError)
            {
                ConcreteSyntaxNode<T> node = new ConcreteSyntaxNode<T>(rule.Key, children);
                result.Root = node;                                
                result.IsEnded = currentPosition >= tokens.Count - 1;
            }

            return result;
        }

        public SyntaxParseResult<T> ParseTerminal(IList<Token<T>> tokens, TerminalClause<T> term, int position)
        {
            SyntaxParseResult<T> result = new SyntaxParseResult<T>();
            result.IsError = !term.Check(tokens[position].TokenID);
            result.EndingPosition = !result.IsError ? position + 1 : position;
            result.Root = new ConcreteSyntaxLeaf<T>(tokens[position]);
            return result;
        }






    }
}