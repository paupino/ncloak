using System;

namespace TiviT.NCloak
{
    [Serializable]
    public class CloakException : Exception
    {
        public CloakException() { }
        public CloakException(string message) : base(message) { }
        public CloakException(string message, Exception inner) : base(message, inner) { }
        protected CloakException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
