using System;
using TiviT.NCloak.Mapping;

namespace TiviT.NCloak
{
    public class CloakContext : ICloakContext
    {
        private readonly InitialisationSettings settings;
        private readonly NameManager nameManager;
        private readonly MappingGraph mappingGraph;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloakContext"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public CloakContext(InitialisationSettings settings)
        {
            //Check for null
            if (settings == null) throw new ArgumentNullException("settings");

            //Make sure they're valid
            settings.Validate();

            //Finally store them
            this.settings = settings;

            //Create the name manager
            nameManager = new NameManager();

            //Create the mapping graph
            mappingGraph = new MappingGraph();
        }

        /// <summary>
        /// Gets the settings.
        /// </summary>
        /// <value>The settings.</value>
        public InitialisationSettings Settings
        {
            get { return settings; }
        }

        /// <summary>
        /// Gets the name manager used to keep track of unique names for each type.
        /// </summary>
        /// <value>The name manager.</value>
        public NameManager NameManager
        {
            get { return nameManager; }
        }

        /// <summary>
        /// Gets the mapping graph.
        /// </summary>
        /// <value>The mapping graph.</value>
        public MappingGraph MappingGraph
        {
            get { return mappingGraph; }
        }
    }
}
