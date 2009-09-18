using TiviT.NCloak.Mapping;
namespace TiviT.NCloak
{
    public interface ICloakContext
    {
        /// <summary>
        /// Gets the settings.
        /// </summary>
        /// <value>The settings.</value>
        InitialisationSettings Settings { get; }

        /// <summary>
        /// Gets the name manager used to keep track of unique names for each type.
        /// </summary>
        /// <value>The name manager.</value>
        NameManager NameManager { get; }

        /// <summary>
        /// Gets the mapping graph.
        /// </summary>
        /// <value>The mapping graph.</value>
        MappingGraph MappingGraph { get; }
    }
}
