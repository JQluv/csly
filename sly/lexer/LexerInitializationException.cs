using System;

namespace sly.lexer
{
    public class LexerInitializationException : Exception
    {
        public LexerInitializationException(string message) : base(message)
        {
        }
    }
}