using System;
using System.Collections.Generic;
namespace TiviT.NCloak
{
    public class NameManager
    {
        private readonly Dictionary<NamingTable, CharacterSet> namingTables;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="NameManager"/> class.
        /// </summary>
        public NameManager()
        {
            namingTables = new Dictionary<NamingTable, CharacterSet>();
        }

        /// <summary>
        /// Sets the start character.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="characterSet">The new character set to use.</param>
        public void SetCharacterSet(NamingTable table, CharacterSet characterSet)
        {
            if (namingTables.ContainsKey(table))
                namingTables[table] = characterSet;
            else
                namingTables.Add(table, characterSet);
        }

        /// <summary>
        /// Generates a new unique name from the naming table.
        /// </summary>
        /// <param name="table">The table to generate a name from.</param>
        /// <returns>A unique name</returns>
        public string GenerateName(NamingTable table)
        {
            //Check the naming table exists
            if (!namingTables.ContainsKey(table))
                SetCharacterSet(table, DefaultCharacterSet);

            //Generate a new name
            if (table == NamingTable.Field) //For fields append an _ to make sure it differs from properties etc
                return "_" + namingTables[table].Generate();
            return namingTables[table].Generate();
        }

        /// <summary>
        /// Gets the default character set.
        /// </summary>
        /// <returns></returns>
        private static CharacterSet DefaultCharacterSet
        {
            get { return new CharacterSet('\u0800', '\u08ff'); }
        }
    }
}
