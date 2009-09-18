namespace TiviT.NCloak.Mapping
{
    public class MemberMapping
    {
        private readonly string memberName;
        private readonly string obfuscatedMemberName;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberMapping"/> class.
        /// </summary>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="obfuscatedMemberName">Name of the obfuscated member.</param>
        public MemberMapping(string memberName, string obfuscatedMemberName)
        {
            this.memberName = memberName;
            this.obfuscatedMemberName = obfuscatedMemberName;
        }

        /// <summary>
        /// Gets the name of the obfuscated member.
        /// </summary>
        /// <value>The name of the obfuscated member.</value>
        public string ObfuscatedMemberName
        {
            get { return obfuscatedMemberName; }
        }

        /// <summary>
        /// Gets the name of the member.
        /// </summary>
        /// <value>The name of the member.</value>
        public string MemberName
        {
            get { return memberName; }
        }
    }
}
