using System;

namespace TiviT.NCloak
{
    [Serializable]
    public class InitialisationException : Exception
    {
        public InitialisationException() { }
        public InitialisationException(string message) : base(message) { }
        public InitialisationException(string message, Exception inner) : base(message, inner) { }
        protected InitialisationException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
