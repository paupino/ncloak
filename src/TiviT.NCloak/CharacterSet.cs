using System;
using System.Text;
using System.Collections.Generic;

namespace TiviT.NCloak
{
    public class CharacterSet
    {
        private readonly char startCharacter;
        private readonly char endCharacter;
		
		private readonly List<char> characterList;

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterSet"/> class.
        /// </summary>
        /// <param name="startCharacter">The start character.</param>
        /// <param name="endCharacter">The end character.</param>
        public CharacterSet(char startCharacter, char endCharacter)
        {
            this.startCharacter = startCharacter;
            this.endCharacter = endCharacter;
			characterList = new List<char>();
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
        /// Gets the end character for this set.
        /// </summary>
        /// <value>The end character.</value>
        public char EndCharacter
        {
            get { return endCharacter; }
        }

        /// <summary>
        /// Generates a new name.
        /// </summary>
        /// <returns>A unique name based upon the character set settings</returns>
        public string Generate()
        {
			//If we're empty then start off the list
			if (characterList.Count == 0)
			{
				characterList.Add(startCharacter);
				return startCharacter.ToString();
			}
			
			//We need to return the character list
			for (int i = 0; i < characterList.Count; i++)
			{
				if (++characterList[i] > endCharacter) {
					//We need to overflow - reset this position
					characterList[i] = startCharacter;
					
					//Check if we're at the end of the list, 
					//if so then add an extra character and get out of here
					if (i == characterList.Count - 1) {
						characterList.Add(startCharacter);
						break;
					}
				}
				else {
					//We're inside our range so we can get out of here
					break;
				}
			}
			
			//Send back our sequence
			//If we've only got a 1 character sequence then don't use a StringBuilder!
			if (characterList.Count == 1)
				return characterList[0].ToString();
			//Build the string backwards
			StringBuilder sequence = new StringBuilder();
			for (int i = characterList.Count - 1; i >= 0; i--)
				sequence.Append(characterList[i]);
			return sequence.ToString();
        }
    }
}
