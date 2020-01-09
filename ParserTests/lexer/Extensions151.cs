using System;
using sly.lexer;
using sly.lexer.fsm;

namespace ParserTests.lexer
{
    public enum RGB
    {
        [Lexeme(GenericToken.Extension)] RGB

    }
    
    public static class Extensions151
    {
        

        public static void AddExtension(RGB token, LexemeAttribute lexem, GenericLexer<RGB> lexer)
        {
            if (token == RGB.RGB)
            {
                NodeCallback<GenericToken> callback = match =>
                {
                    match.Properties[GenericLexer<Extensions>.DerivedToken] = RGB.RGB;
                    return match;
                };

                var fsmBuilder = lexer.FSMBuilder;

                fsmBuilder.GoTo(GenericLexer<Extensions>.start)
                    .Transition('#')
                    .Mark("start_date")
                    .RepetitionTransition(4, "[0-9,a-f,A-F]")
                    .End(GenericToken.Extension)
                    .CallBack(callback)
                    .RepetitionTransition(2,"[0-9,a-f,A-F]")
                    .End(GenericToken.Extension)
                    .CallBack(callback);
            }
        }
    }
}