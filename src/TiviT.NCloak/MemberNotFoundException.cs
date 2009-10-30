using System;

namespace TiviT.NCloak
{
    [Serializable]
    public class MemberNotFoundException : CloakException
    {
        public MemberNotFoundException() { }
        public MemberNotFoundException(string message) : base(message) { }
        public MemberNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected MemberNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
