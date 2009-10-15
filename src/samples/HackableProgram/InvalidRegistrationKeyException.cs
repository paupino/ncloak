using System;

namespace HackableProgram
{
    [Serializable]
    public class InvalidRegistrationKeyException : Exception
    {
        public InvalidRegistrationKeyException() { }
        public InvalidRegistrationKeyException(string message) : base(message) { }
        public InvalidRegistrationKeyException(string message, Exception inner) : base(message, inner) { }
        protected InvalidRegistrationKeyException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
