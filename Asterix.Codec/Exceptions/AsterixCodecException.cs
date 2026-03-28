namespace Asterix.Codec.Exceptions;

/// <summary>
/// Base class for all exceptions thrown by the Asterix.Codec library.
/// </summary>
public abstract class AsterixCodecException : Exception
{
    protected AsterixCodecException(string message) : base(message) { }
    protected AsterixCodecException(string message, Exception inner) : base(message, inner) { }
}
