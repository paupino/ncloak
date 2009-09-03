using System;

namespace TiviT.NCloak
{
    public class CharacterSet
    {
        private readonly char startCharacter;
        private readonly char endCharacter;

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterSet"/> class.
        /// </summary>
        /// <param name="startCharacter">The start character.</param>
        /// <param name="endCharacter">The end character.</param>
        public CharacterSet(char startCharacter, char endCharacter)
        {
            this.startCharacter = startCharacter;
            this.endCharacter = endCharacter;
            CurrentCharacter = startCharacter;
            Prefix = String.Empty;
        }

        /// <summary>
        /// Gets or sets the prefix for names in this set.
        /// </summary>
        /// <value>The prefix.</value>
        public string Prefix { get; private set; }

        /// <summary>
        /// Gets or sets the current character being used.
        /// </summary>
        /// <value>The current character.</value>
        public char CurrentCharacter { get; private set; }

        /// <summary>
        /// Gets the end character for this set.
        /// </summary>
        /// <value>The end character.</value>
        public char EndCharacter
        {
            get { return endCharacter; }
        }

        /// <summary>
        /// Gets the start character for this set.
        /// </summary>
        /// <value>The start character.</value>
        public char StartCharacter
        {
            get { return startCharacter; }
        }

        /// <summary>
        /// Generates a new name.
        /// </summary>
        /// <returns>A unique name based upon the character set settings</returns>
        public string Generate()
        {
            //Get the name
            string newName = String.Format("{0}{1}", Prefix, CurrentCharacter);

            //Increment our state
            CurrentCharacter++;

            //Check if we're over our quota
            if (CurrentCharacter > EndCharacter)
            {
                //We need to roll over to a new prefix
                if (String.IsNullOrEmpty(Prefix))
                    Prefix = startCharacter.ToString();
                else
                {
                    //TODO - we need a proper implementation here
                    Prefix = Prefix + startCharacter;
                }
            }

            //Return it
            return newName;
        }
    }
}
